import array
import itertools
import math
import random
import struct

from PyQt5.QtCore import QEvent, QPoint, QRect, Qt
from PyQt5.QtGui import (
        QGuiApplication, QWindow, 
        QMatrix4x4, QVector3D, QVector4D, QVector2D,
        QOpenGLContext, QOpenGLVersionProfile, QOpenGLBuffer,
        QOpenGLDebugLogger, QOpenGLVertexArrayObject, QOpenGLTexture,
        QOpenGLShader, QOpenGLShaderProgram, QOpenGLPaintDevice, 
        QOpenGLFramebufferObject, QOpenGLFramebufferObjectFormat,
        QSurfaceFormat, QPainter, QColor, QFont, QFontMetrics
        )

''' Invokes rendering OpenGL commands. '''
class Renderer(object):

    ''' logical_resources.Resources '''
    logicalResources = None

    ''' opengl_resources.Resources '''
    openglResources = None

    ''' Dictionary of all GLSL unifroms '''
    uniforms = None

    ''' Use multisampling '''
    multisample = False

    def __init__(self, logicalResources, openglResources):
        self.logicalResources = logicalResources
        self.openglResources = openglResources

        self.uniforms = {}
        self.uniforms['NormalMapCoordinatesShift1'] = QVector3D(0, 0, 0)
        self.uniforms['NormalMapCoordinatesShift2'] = QVector3D(0, 0, 0)
        self.uniforms['NormalMapCoordinatesShift3'] = QVector3D(0, 0, 0)
        self.uniforms['NormalMapCoordinatesShift4'] = QVector3D(0, 0, 0)
        self.uniforms['ZRange'] = QVector2D(0, 55/30)
        self.uniforms['Delta'] = QVector2D(1/self.logicalResources.m, 1/self.logicalResources.n)
        self.uniforms['Alpha'] = 1.0
        self.uniforms['RefractionMagnitude'] = 1.0
        self.uniforms['NormalBumpMagnitude'] = 0.6
        
        self.uniforms['Ambient'] = QVector3D( 0.2, 0.0, 0.2)

        self.uniforms['LightPosition'] = self.logicalResources.lightSourcePosition

        self.uniforms['ConstantAttenuation'] =  0.1
        self.uniforms['LinearAttenuation'] =  0.1
        self.uniforms['QuadraticAttenuation'] =  0.001

    def render(self, gl, width, height, render_water=False):
        ''' Updates uniforms and renders whole scene '''

        mvmatrix = self.logicalResources.mvmatrix()
        self.uniforms['Eye'] = self.logicalResources.eye

        for i in range(1,5):
            name = 'NormalMapCoordinatesShift{}'.format(i)
            self.uniforms[name] += 0.005 * QVector3D(random.random(), random.random(), random.random())
            if min(self.uniforms[name].x(), self.uniforms[name].y(), self.uniforms[name].z()) > 4.0:
                self.uniforms[name] -= QVector3D(4, 4, 4)

        self.uniforms['SelectedLandscapeCell'] = QVector2D(*self.logicalResources.selectedLandscapeCell)
        self.uniforms['Dimensions'] = QVector2D(max(1, self.logicalResources.m-1), max(1, self.logicalResources.n-1))

        gl.glEnable(gl.GL_CULL_FACE)
        gl.glEnable(gl.GL_DEPTH_TEST)
        if self.multisample:
            gl.glEnable(gl.GL_MULTISAMPLE)
        else:
            gl.glDisable(gl.GL_MULTISAMPLE)
        gl.glEnable(gl.GL_BLEND)
        gl.glCullFace(gl.GL_BACK)

        self.uniforms['MVPMatrix'] = self.logicalResources.mvmatrix(width, height) 
        self.uniforms['DepthMVPMatrix'] = QMatrix4x4(
                0.5, 0.0, 0.0, 0.5,
                0.0, 0.5, 0.0, 0.5,
                0.0, 0.0, 0.5, 0.5,
                0.0, 0.0, 0.0, 1.0) * self.uniforms['MVPMatrix'] 

        self.renderDepth(gl)
        if render_water:
            self.renderWaterRefraction(gl)

        gl.glViewport(0, 0, width, height)
        self.renderLandscape(gl, self.openglResources.landscapeProgram)
        gl.glBlendFunc(gl.GL_SRC_ALPHA, gl.GL_ONE_MINUS_SRC_ALPHA)
        if render_water:
             self.renderWater(gl, self.openglResources.waterProgram, RefractionMagnitude=0.7, NormalBumpMagnitude=0.6)
             pass

        gl.glDisable(gl.GL_CULL_FACE)
        gl.glDisable(gl.GL_DEPTH_TEST)

    def renderDepth(self, gl):
        """ Renders depth info. Additionaly renders indexes of 
            landscape cell as color, to retrieve currently 
            clicked/pointed cells.
        """

        assert self.openglResources.depthFramebuffer.bind()

        gl.glViewport(0, 0, self.openglResources.depthFramebuffer.width(), self.openglResources.depthFramebuffer.height())

        gl.glClearColor(0.0, 0.0, 0.0, 0.0)
        gl.glClear(gl.GL_COLOR_BUFFER_BIT | gl.GL_DEPTH_BUFFER_BIT)
        gl.glClearColor(*self.logicalResources.clearColor)

        gl.glEnable(gl.GL_POLYGON_OFFSET_FILL)
        gl.glPolygonOffset(2.0, 4.0);
        self.renderLandscape(gl, self.openglResources.depthProgram)
        gl.glDisable(gl.GL_POLYGON_OFFSET_FILL)

        assert self.openglResources.depthFramebuffer.release()


    def renderWaterRefraction(self, gl):
        ''' Renders simplified scene for use as refraction '''
        assert self.openglResources.refractionFramebuffer.bind()

        gl.glViewport(0, 0, self.openglResources.refractionFramebuffer.width(), self.openglResources.refractionFramebuffer.height())
        gl.glClear(gl.GL_COLOR_BUFFER_BIT | gl.GL_DEPTH_BUFFER_BIT);

        self.renderLandscape(gl, self.openglResources.landscapeProgram)
        gl.glBlendFunc(gl.GL_SRC_ALPHA, gl.GL_ONE_MINUS_SRC_ALPHA)
        gl.glDisable(gl.GL_DEPTH_TEST)
        self.renderWater(gl, self.openglResources.waterRefractionProgram, Alpha=0.5, RefractionMagnitude=0.0)
        gl.glEnable(gl.GL_DEPTH_TEST)

        assert self.openglResources.refractionFramebuffer.release()

    def setUniforms(self, program, kwuniforms={}):
        ''' Sets GLSL *program* uniforms '''
        for k, v in self.uniforms.items():
            if k not in kwuniforms:
                program.setUniformValue(k, v)
        for k, v in kwuniforms.items():
            program.setUniformValue(k, v)

    def renderLandscape(self, gl, program, **kwuniforms):
        ''' Renders landscape with *program* '''
        program.bind()
        self.setUniforms(program, kwuniforms)

        gl.glActiveTexture(gl.GL_TEXTURE2)
        gl.glBindTexture(gl.GL_TEXTURE_2D, self.openglResources.depthTexture.textureId())
        program.setUniformValue('DepthMap', 2)

        self.openglResources.landscapeVBO.bind()
        vertexPosition = program.attributeLocation('vertexPosition')
        if vertexPosition >= 0:
            gl.glVertexAttribPointer(vertexPosition, 3, gl.GL_FLOAT, gl.GL_FALSE, 0, 0)
            program.enableAttributeArray(vertexPosition)

        vertexNormal = program.attributeLocation('vertexNormal')
        if vertexNormal >= 0:
            gl.glVertexAttribPointer(vertexNormal, 3, gl.GL_FLOAT, gl.GL_FALSE, 0, self.openglResources.numberOfLandscapeVertices*3*4)
            program.enableAttributeArray(vertexNormal)
        
        indexInMatrix = program.attributeLocation('vertexIndexInMatrix')
        if indexInMatrix >= 0:
            gl.glVertexAttribPointer(indexInMatrix, 2, gl.GL_FLOAT, gl.GL_FALSE, 0, self.openglResources.numberOfLandscapeVertices*3*4*2)
            program.enableAttributeArray(indexInMatrix)

        gl.glDrawArrays(gl.GL_TRIANGLES, 0, self.openglResources.numberOfLandscapeVertices)
        self.openglResources.landscapeVBO.release()
        

        program.release()

    def renderWater(self, gl, program, **kwuniforms):
        ''' Renders water with *program* '''
        program.bind()
        self.setUniforms(program, kwuniforms)

        gl.glActiveTexture(gl.GL_TEXTURE0)
        self.openglResources.refractionNormalMap.bind()
        program.setUniformValue('NormalMap', 0)

        gl.glActiveTexture(gl.GL_TEXTURE1)
        gl.glBindTexture(gl.GL_TEXTURE_2D, self.openglResources.refractionFramebuffer.texture())
        program.setUniformValue('Refraction', 1)

        gl.glActiveTexture(gl.GL_TEXTURE2)
        gl.glBindTexture(gl.GL_TEXTURE_2D, self.openglResources.depthTexture.textureId())
        program.setUniformValue('DepthMap', 2)

        gl.glActiveTexture(gl.GL_TEXTURE3)
        gl.glBindTexture(gl.GL_TEXTURE_2D, self.openglResources.heightsTexture.textureId())
        program.setUniformValue('HeightMatrix', 3)

        self.openglResources.waterVBO.bind()
        vertexPosition = program.attributeLocation('vertexPosition')
        if vertexPosition >= 0:
            gl.glVertexAttribPointer(vertexPosition, 3, gl.GL_FLOAT, gl.GL_FALSE, 0, 0)
            program.enableAttributeArray(vertexPosition)

        vertexNormal = program.attributeLocation('vertexNormal')
        if vertexNormal >= 0:
            gl.glVertexAttribPointer(vertexNormal, 3, gl.GL_FLOAT, gl.GL_FALSE, 0, self.openglResources.numberOfWaterVertices*3*4)
            program.enableAttributeArray(vertexNormal)

        indexInMatrix = program.attributeLocation('vertexIndexInMatrix')
        if indexInMatrix >= 0:
            gl.glVertexAttribPointer(indexInMatrix, 2, gl.GL_FLOAT, gl.GL_FALSE, 0, self.openglResources.numberOfWaterVertices*3*4*2)
            program.enableAttributeArray(indexInMatrix)

        
        gl.glDrawArrays(gl.GL_TRIANGLES, 0, self.openglResources.numberOfWaterVertices)
        self.openglResources.waterVBO.release()

        for i in range(7):
            gl.glActiveTexture(gl.GL_TEXTURE0 + i)
            gl.glBindTexture(gl.GL_TEXTURE_2D, 0)

        program.release()


