#!/usr/bin/env python3

import array
import itertools
import math
import random
import struct
import time

from PyQt5.QtCore import QEvent, QPoint, QRect, QRectF, Qt
from PyQt5.QtWidgets import QApplication, QMessageBox
from PyQt5.QtGui import (
        QWindow, QImage, QMatrix4x4, QVector3D, QVector4D,
        QOpenGLContext, QOpenGLVersionProfile, QOpenGLBuffer,
        QOpenGLDebugLogger, QOpenGLVertexArrayObject, QOpenGLTexture,
        QOpenGLShader, QOpenGLShaderProgram, QOpenGLPaintDevice, 
        QOpenGLFramebufferObject, QOpenGLFramebufferObjectFormat,
        QSurfaceFormat, QPainter, QColor, QFont, QFontMetrics
        )
import PyQt5.QtGui as QtGui


import openglwindow
import logical_resources
import opengl_resources
import renderer
import solver
app = None
class WaterWindow(openglwindow.OpenGLWindow):

    """ logical_resources.Resources """
    logicalResources = None

    """ opengl_resources.Resources """
    openglResources = None

    """ Object dealing with rendering logic. """
    renderer = None

    """ Object implementing core algo. """
    solver = None

    """ Time, when last algo step had place. """
    timeOfLastSolverStep = None

    drawRefraction = False

    drawDepth = False

    def __init__(self):
        super(WaterWindow, self).__init__()

        self.logicalResources = logical_resources.Resources()
        self.openglResources = opengl_resources.Resources(self.logicalResources)
        self.renderer = renderer.Renderer(self.logicalResources, self.openglResources)

    def initialize(self, gl):
        gl.glClearColor(*self.logicalResources.clearColor)

        self.openglResources.initialize(gl)

    def keyPressEvent(self, event):

        if event.key() == Qt.Key_Enter or event.key() == Qt.Key_Return:
            self.m_context.makeCurrent(self)
            if self.solver:
#               If solver alredy working - go to end in one step                
                self.stepSolver(self.m_gl, proceedTillEnd=True)
            else:
                self.solver = solver.Solver(self.logicalResources.landscapeHeightsMatrix).compute()
                self.timeOfLastSolverStep = time.time() 
        elif event.key() == Qt.Key_Escape:
            self.logicalResources.saveLandscapeHeightsMatrix()
            app.exit()
        elif event.key() == Qt.Key_Space:
#           Clearing water            
            self.solver = None
            self.timeOfLastSolverStep = None
            self.m_context.makeCurrent(self)
            self.logicalResources.generateWaterHeightsMatrix()
            self.openglResources.updateMeshesAndHeightsTexture(self.m_gl, landscape=False)
        elif event.key() == Qt.Key_PageUp:
            self.logicalResources.moveForwardBackward(0.25)
        elif event.key() == Qt.Key_PageDown:
            self.logicalResources.moveForwardBackward(-0.25)
        elif event.key() == Qt.Key_M:
            self.renderer.multisample = not self.renderer.multisample
        elif event.key() == Qt.Key_D:
            self.drawRefraction = False
            self.drawDepth = not self.drawDepth
        elif event.key() == Qt.Key_R:
            self.drawDepth = False
            self.drawRefraction = not self.drawRefraction
        elif event.key() == Qt.Key_F1:
            QMessageBox.information(None, 'Controls', """

Mouse wheel, PageUp/PageDown - zoom

Left mouse button + mouse movement - rotation

Right mouse button + mouse wheel - change landscape cell height
    (works only in absence of water)

Enter - start algo. Second Enter skips algo till end.

Space - clear water from landscape.

'+'/'-' - add, remove rows. (there should be no water)

Shift + '+'/'-' - add, remove columns. (same)

Esc - exit, obviously.

M - toggle multisampling.

D, R - draw additional framebuffers (depth and refraction).
            
            """)
        elif self.solver is None:
#           Resizing landscape            

            expandKeys = {
                    Qt.Key_Equal: (1, 0), 
                    Qt.Key_Minus:(-1, 0),
                    Qt.Key_Plus: (0, 1),
                    Qt.Key_Underscore: (0, -1)
                    }
            expand = expandKeys.get(event.key())
            if expand is not None:
                self.logicalResources.expandLandscapeHeightsMatrix(*expand)
                self.m_context.makeCurrent(self)
                self.openglResources.updateMeshesAndHeightsTexture(self.m_gl)

        self.renderLater()

    def mouseMoveEvent(self, event):
        dx = event.x() - self.logicalResources.lastMousePosition.x()
        dy = event.y() - self.logicalResources.lastMousePosition.y()

        rotated = True
        q = 1
        if event.buttons() & Qt.LeftButton:
            self.logicalResources.rotateUpDown(q*dy)
            self.logicalResources.rotateLeftRight(-q*dx)
        else:
            rotated = False

        self.logicalResources.lastMousePosition = event.pos()
        if rotated:
            self.renderLater()

    def wheelEvent(self, event):
        dy = event.angleDelta().y()
        if not event.buttons() & (Qt.LeftButton | Qt.RightButton):
            dy = math.copysign(min(abs(dy), 100), dy)
            self.logicalResources.moveForwardBackward(dy / 200.0)
        elif self.solver is None:
#           Searching currently pointed landscape cell and changing it's height            
            x = int((event.x() / self.width()) * self.openglResources.depthFramebuffer.width())
            y = int((event.y() / self.height()) * self.openglResources.depthFramebuffer.height())
            
            image = self.openglResources.depthFramebuffer.toImage()
            pixel = image.pixel(x, y)
            if QtGui.qAlpha(pixel) != 0:
#               In depthFramebuffer color texture indexes info coded as color                
#               Maybe not so genious and handy, but pretty beautiful ;-)                
                j = int((QtGui.qRed(pixel) / 256) * self.logicalResources.m)
                i = int((QtGui.qGreen(pixel) / 256) * self.logicalResources.n)
                self.logicalResources.changeLandscapeHeight(i, j, int(math.copysign(1, dy)))

                self.m_context.makeCurrent(self)
                self.openglResources.updateMeshesAndHeightsTexture(self.m_gl)

        self.renderLater()

    def stepSolver(self, gl, proceedTillEnd=False):
        ''' Proceeds algo evaluation '''
        heightsUpdated = False

        while (self.timeOfLastSolverStep is not None
                and (proceedTillEnd or time.time() - self.timeOfLastSolverStep >= 0.05)):
            step = next(self.solver, None)
            if step is None:
                self.timeOfLastSolverStep = None
                break

            stepWasMeaningful = True
            if step[0] == 'Select':
                self.logicalResources.selectedLandscapeCell = step[1][::-1]
            elif step[0] == 'Zero':
                p = step[1]
                self.logicalResources.changeWaterHeight(p[0], p[1], 0)
                heightsUpdated = True
            elif step[0] == 'Lower height':
                p = step[1]
                self.logicalResources.changeWaterHeight(p[0], p[1], step[2])
                heightsUpdated = True
            else:
                stepWasMeaningful = False

            if stepWasMeaningful:
                self.timeOfLastSolverStep = time.time()
        
        if heightsUpdated:
            self.openglResources.updateMeshesAndHeightsTexture(gl, landscape=False)

    def render(self, gl):
        if self.solver is not None:
            self.stepSolver(gl)

        self.renderer.render(gl, self.width(), self.height(), render_water=(self.solver is not None))

    def paint(self, painter):
        ''' Draws additional data over window '''
        if self.drawRefraction:
            target = QRectF(0, 0, 256, 256)
            source = QRectF(0, 0, self.openglResources.refractionFramebuffer.width()
                                , self.openglResources.refractionFramebuffer.height())
            painter.drawImage(target, self.openglResources.refractionFramebuffer.toImage(), source)
            painter.drawRect(target)

        if self.drawDepth:
            target = QRectF(0, 0, 256, 256)
            source = QRectF(0, 0, self.openglResources.depthFramebuffer.width()
                                , self.openglResources.depthFramebuffer.height())
            painter.drawImage(target, self.openglResources.depthFramebuffer.toImage(), source)
            painter.drawRect(target)

        self.drawInstructions(painter)

    def drawInstructions(self, painter):
        text = 'Press F1 for controls'
        metrics = QFontMetrics(QFont("Times", 12, QFont.Bold))
        border = max(4, metrics.leading())

        rect = metrics.boundingRect(0, 0, self.width() - 2*border,
                int(self.height()*0.125), Qt.AlignCenter,
                text)
        painter.setRenderHint(QPainter.TextAntialiasing)
        painter.fillRect(QRect(0, 0, self.width(), rect.height() + 2*border), QColor(0, 0, 0, 127))
        painter.setPen(Qt.white)
        painter.fillRect(QRect(0, 0, self.width(), rect.height() + 2*border), QColor(0, 0, 0, 127))
        painter.drawText((self.width() - rect.width())/2, border, rect.width(),
                rect.height(), Qt.AlignCenter | Qt.TextWordWrap, text)



if __name__ == '__main__':

    import sys

    app = QApplication(sys.argv)

    format = QSurfaceFormat()
    format.setSamples(4)
    format.setDepthBufferSize(24)
    format.setOption(QSurfaceFormat.DebugContext)

    window = WaterWindow()
    window.setFormat(format)
    window.resize(640, 480)
    window.showMaximized()

    window.setAnimating(True)

    sys.exit(app.exec_())
