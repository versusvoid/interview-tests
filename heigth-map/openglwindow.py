''' Copypasted from pyqt examples '''

from PyQt5.QtCore import QEvent, QPoint, QRect, QCoreApplication, Qt
from PyQt5.QtGui import (
        QGuiApplication, QWindow, 
        QMatrix4x4, QVector3D, 
        QOpenGLContext, QOpenGLVersionProfile, QOpenGLBuffer,
        QOpenGLDebugLogger,
        QOpenGLShader, QOpenGLShaderProgram, QSurfaceFormat, 
        QPainter, QOpenGLPaintDevice, QColor, QFont, QFontMetrics
        )

# Helps us to catch ^C signal.
def exitOnKeyboardInterrupt(method):
    def method_(*args, **kwargs):
        try:
            method(*args, **kwargs)
        except KeyboardInterrupt:
            QCoreApplication.exit()

    return method_

class OpenGLWindow(QWindow):
    def __init__(self, parent=None):
        super(OpenGLWindow, self).__init__(parent)

        self.m_update_pending = False
        self.m_animating = False
        self.m_context = None
        self.m_device = None
        self.m_gl = None
        self.logger = None

        self.setSurfaceType(QWindow.OpenGLSurface)

    def initialize(self, gl):
        pass

    def setAnimating(self, animating):
        self.m_animating = animating

        if animating:
            self.renderLater()

    def renderLater(self):
        if not self.m_update_pending:
            self.m_update_pending = True
            QGuiApplication.postEvent(self, QEvent(QEvent.UpdateRequest))

    def paint(self, painter):
        pass

    def render(self, gl):
        pass

    def addGlFunctuins(self, GL, functions):
        for function, arguments in functions.items():
            GL[function].restype = None
            GL[function].argtypes = arguments
            setattr(self.m_gl, function, GL[function])

    @exitOnKeyboardInterrupt
    def renderNow(self):
        if not self.isExposed():
            return

        self.m_update_pending = False

        needsInitialize = False

        if self.m_context is None:
            self.m_context = QOpenGLContext(self)
            self.m_context.setFormat(self.requestedFormat())
            self.m_context.create()

            needsInitialize = True

        self.m_context.makeCurrent(self)

        if needsInitialize:
#           Sorry, no support for higher versions for now.
            profile = QOpenGLVersionProfile()
            profile.setVersion(2, 0)

            self.m_gl = self.m_context.versionFunctions(profile)
            self.m_gl.initializeOpenGLFunctions()

            #print(self.m_context.hasExtension('GL_EXT_framebuffer_object'))
            #print(self.m_context.hasExtension('GL_ARB_texture_float'))
            #print(*sorted(self.m_context.extensions()), sep='\n')

#           Small hack. Guess noone mind?            
            import ctypes
            import ctypes.util
            GL = ctypes.CDLL(ctypes.util.find_library('GL'))

            self.addGlFunctuins(GL, {
                'glFramebufferTexture2D': (ctypes.c_uint, ctypes.c_uint, ctypes.c_uint, ctypes.c_uint, ctypes.c_int)
                })

            self.logger = QOpenGLDebugLogger()
            self.logger.initialize()
            self.logger.loggedMessages()
            self.logger.messageLogged.connect(self.handleLoggedMassage)
            self.logger.startLogging()

            self.initialize(self.m_gl)
        
        if not self.m_device:
            self.m_device = QOpenGLPaintDevice()

        self.m_gl.glClear(self.m_gl.GL_COLOR_BUFFER_BIT | self.m_gl.GL_DEPTH_BUFFER_BIT);

        self.m_device.setSize(self.size())

        painter = QPainter(self.m_device)


        painter.beginNativePainting()
        self.render(self.m_gl)
        painter.endNativePainting()

        self.paint(painter)

        self.m_context.swapBuffers(self)

        if self.m_animating:
            self.renderLater()

    def handleLoggedMassage(self, message):
#       This three really annoyng and brings no useful info =\        
        if not (message.message().find('Use glDrawRangeElements() to avoid this.') > -1 or 
                message.message().find('CPU mapping a busy miptree') > -1 or
                message.message().find('Flushing before mapping a referenced bo.') > -1
                ):
            print(message.message().strip())

    def event(self, event):
        if event.type() == QEvent.UpdateRequest:
            self.renderNow()
            return True

        return super(OpenGLWindow, self).event(event)

    def exposeEvent(self, event):
        self.renderNow()

    def resizeEvent(self, event):
        self.renderNow()



