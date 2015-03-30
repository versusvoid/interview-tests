import array
from itertools import repeat, islice, chain
import math
import random
import struct
import os.path

from PyQt5.QtGui import (
        QImage, QOpenGLFramebufferObject,
        QOpenGLBuffer, QOpenGLTexture,
        QOpenGLShader, QOpenGLShaderProgram
        )

""" Landscape height scale """
ZScale = 1/60

""" Keeps opengl objects """
class Resources(object):

    directory = os.path.dirname(__file__)

    ''' GLSL program to draw water '''
    waterProgram = None
    ''' Vertex Buffer Object with water mesh '''
    waterVBO = None
    ''' Number of verticies in water mesh '''
    numberOfWaterVertices = None

    ''' GLSL program to draw simplified scene for use 
        as refraction
    '''
    waterRefractionProgram = None
    ''' Framebuffer to draw simplified scene onto '''
    refractionFramebuffer = None
    ''' Water normal map, primary used as bump for
        refraction.
    '''
    refractionNormalMap = None
    
    ''' GLSL program to draw depth info of scene '''
    depthProgram = None
    ''' Framebuffer to draw depth info onto '''
    depthFramebuffer = None
    ''' Texture of depth component '''
    depthTexture = None

    ''' GLSL program to draw landscape '''
    landscapeProgram = None
    ''' Vertex Buffer Object with landscape mesh '''
    landscapeVBO = None
    ''' Number of verticies in landscape mesh '''
    numberOfLandscapeVertices = None

    ''' Texture with information about landscape and 
        water heights. 
        Red component stands for landscape height,
        green for water.
    '''
    heightsTexture = None

    ''' logical_resources.Resources '''
    logicalResources = None

    def __init__(self, logicalResources):
        self.logicalResources = logicalResources


    def initialize(self, gl):
        """
        Creating resources, which require OpenGL context
        """

        self.waterProgram = self.linkProgram(gl, 'water')
        self.waterVBO = QOpenGLBuffer(QOpenGLBuffer.VertexBuffer)
        assert self.waterVBO.create(), "Can't create water vertex buffer =\\"
        self.waterVBO.setUsagePattern(QOpenGLBuffer.DynamicDraw)

        self.waterRefractionProgram = self.linkProgram(gl, 'water-refraction')
        self.refractionFramebuffer = self.createFramebuffer(gl, 512, depth=True)
        self.refractionNormalMap = self.createTexture(gl, wrapMode=QOpenGLTexture.Repeat, filename='normalmap.bmp')

        self.depthProgram = self.linkProgram(gl, 'depth')
        self.depthFramebuffer = self.createFramebuffer(gl, 512)
        self.depthTexture = self.createTexture(gl, self.depthFramebuffer.width(), format=QOpenGLTexture.D32F, allocate=False,
                GL_TEXTURE_COMPARE_MODE=gl.GL_COMPARE_REF_TO_TEXTURE,
                GL_TEXTURE_COMPARE_FUNC=gl.GL_LESS)
        self.depthTexture.bind()
        gl.glTexImage2D(gl.GL_TEXTURE_2D, 0, gl.GL_DEPTH_COMPONENT32, 
                self.depthFramebuffer.width(), self.depthFramebuffer.height(), 
                0, gl.GL_DEPTH_COMPONENT, gl.GL_FLOAT, None)
        self.depthTexture.release()
        assert self.depthFramebuffer.bind()
        gl.glFramebufferTexture2D(gl.GL_FRAMEBUFFER, gl.GL_DEPTH_ATTACHMENT, gl.GL_TEXTURE_2D, self.depthTexture.textureId(), 0)
        assert self.depthFramebuffer.release()

        self.landscapeProgram = self.linkProgram(gl, 'landscape')
        self.landscapeVBO = QOpenGLBuffer(QOpenGLBuffer.VertexBuffer)
        assert self.landscapeVBO.create(), "Can't create water vertex buffer =\\"
        self.landscapeVBO.setUsagePattern(QOpenGLBuffer.DynamicDraw)

        self.heightsTexture = self.createTexture(gl, self.logicalResources.m, self.logicalResources.n, 
                format=QOpenGLTexture.RG32F, filter=QOpenGLTexture.Nearest)
       
        self.updateMeshesAndHeightsTexture(gl)

    def updateMeshesAndHeightsTexture(self, gl, water=True, landscape=True):
        ''' Updates water and/or landscape mesh when they have changed '''
        if landscape:
            self.numberOfLandscapeVertices = self.generateLandscapeMesh(gl, self.landscapeVBO)
        if water:
            self.numberOfWaterVertices = self.generateWaterMesh(gl, self.waterVBO)
        if water or landscape:
            self.updateHeightsTexture(gl)
 
    def updateHeightsTexture(self, gl):
        """
        Updates texture with landscape and water heights info
        """

        if (self.heightsTexture.width() != self.logicalResources.n or 
                self.heightsTexture.height() != self.logicalResources.m):
            self.heightsTexture.destroy()

            self.heightsTexture = self.createTexture(gl, self.logicalResources.m, self.logicalResources.n, 
                    format=QOpenGLTexture.RG32F, filter=QOpenGLTexture.Nearest)
    
        data = []
        for i in range(self.logicalResources.n):
            for j in range(self.logicalResources.m):
                data.extend([ self.logicalResources.landscapeHeightsMatrix[i][j] * ZScale
                            , self.logicalResources.waterHeightsMatrix[i][j] * ZScale])
        data = struct.pack('{}f'.format(len(data)), *data)

        self.heightsTexture.setData(QOpenGLTexture.RG, QOpenGLTexture.Float32, data)


    def loadFile(self, name):
        ''' Loads whole file content as single string '''
        with open(name, 'r') as f: return ''.join(f)
    
    def loadShaders(self, name):
        ''' Loads vertex and fragment shader '''
        vertexShaderSource = self.loadFile('{}/shaders/{}.vert'.format(Resources.directory, name))
        fragmentShaderSource = self.loadFile('{}/shaders/{}.frag'.format(Resources.directory, name))

        return vertexShaderSource, fragmentShaderSource

    def linkProgram(self, gl, name, **kwargs):
        ''' Links GLSL program from *name*.vert and *name*.frag shaders '''
        program = QOpenGLShaderProgram()

        vertexShader, fragmentShader = self.loadShaders(name)
        program.addShaderFromSourceCode(QOpenGLShader.Vertex,
                vertexShader)
        program.addShaderFromSourceCode(QOpenGLShader.Fragment,
                fragmentShader)

        assert program.link(), "Can't link ShaderProgram"
        assert program.bind(), "Can't bind ShaderProgram for initialization"

        for k, v in kwargs.items():
            if type(v) in [list, tuple]:
                program.setUniformValue(k, *v)
            else:
                program.setUniformValue(k, v)

        program.release()

        return program



    def createFramebuffer(self, gl, dim, depth=False, filter=None, internalFormat=None, format=None, type=None):
        ''' Creates framebuffer object with required parameters '''
        if filter is None: filter = gl.GL_LINEAR
        framebuffer = QOpenGLFramebufferObject(dim, dim)
        if depth: framebuffer.setAttachment(QOpenGLFramebufferObject.Depth)
        textureId = framebuffer.texture()
        assert textureId >= 0

        gl.glBindTexture(gl.GL_TEXTURE_2D, textureId)
        gl.glTexParameteri(gl.GL_TEXTURE_2D, gl.GL_TEXTURE_MAG_FILTER, filter)
        gl.glTexParameteri(gl.GL_TEXTURE_2D, gl.GL_TEXTURE_MIN_FILTER, filter)
        if internalFormat and format and type:
            gl.glTexImage2D(gl.GL_TEXTURE_2D, 0, internalFormat, dim, dim, 0, format, type, None)
        gl.glBindTexture(gl.GL_TEXTURE_2D, 0)

        return framebuffer

    def createTexture(self, gl, width=None, height=None, 
            wrapMode=QOpenGLTexture.ClampToEdge, format=QOpenGLTexture.RGBA8U, filter=QOpenGLTexture.Linear, 
            filename=None, allocate=True, **kwparams):
        ''' Creates texture object with required parameters '''
        assert width is not None or filename is not None
        if height is None: height = width

        if filename:
            texture = QOpenGLTexture(QImage('{}/{}'.format(Resources.directory, filename)).mirrored(), QOpenGLTexture.DontGenerateMipMaps)
        else:
            texture = QOpenGLTexture(QOpenGLTexture.Target2D)
            texture.setFormat(format)
            texture.setSize(width, height)
            if allocate: texture.allocateStorage()

        texture.setMinificationFilter(filter)
        texture.setMagnificationFilter(filter)
        texture.setWrapMode(wrapMode)

        assert texture.create()

        texture.bind()
        for k, v in kwparams.items():
            gl.glTexParameteri(gl.GL_TEXTURE_2D, getattr(gl, k), v)
        texture.release()

        return texture


      
    def safeLandscapeHeightsMatrixIndexer(self, i, j):
        ''' Accesses landscapeHeightsMatrix preventing out of bound errors.
            Used as z0 in *generateLandscapeMesh*.
        '''
        return self.logicalResources.landscapeHeightsMatrix[max(0, min(i, self.logicalResources.n-1))][max(0, min(j, self.logicalResources.m-1))]

    def generateWaterMesh(self, gl, vbo):
        ''' Generates water mesh '''
#       reusing existing code        
        return self.generateLandscapeMesh(gl, vbo, self.logicalResources.waterHeightsMatrix, self.safeLandscapeHeightsMatrixIndexer)

   
    def generateLandscapeMesh(self, gl, vbo, matrix=None, z0=0):
        ''' Generates landscape mesh, and stores in *vbo*.
            Due similiarity also used for generating water mesh. 
        '''
        if matrix is None: matrix = self.logicalResources.landscapeHeightsMatrix

        vbo.bind()

        vertices, normals, indexiesInMatrix = self.buildMeshTriangles(matrix, z0)
        numberOfLandscapeVertices = len(vertices) // 3
        vertices = struct.pack('{}f'.format(len(vertices)), *vertices)
        normals = struct.pack('{}f'.format(len(normals)), *normals)
        indexiesInMatrix = struct.pack('{}f'.format(len(indexiesInMatrix)), *indexiesInMatrix)
        assert numberOfLandscapeVertices*3*4 == len(vertices)
        size = len(vertices) + len(normals) + len(indexiesInMatrix)

        gl.glBufferData(gl.GL_ARRAY_BUFFER, size, None, gl.GL_DYNAMIC_DRAW)
        gl.glBufferSubData(gl.GL_ARRAY_BUFFER, 0, len(vertices), vertices)
        gl.glBufferSubData(gl.GL_ARRAY_BUFFER, len(vertices), len(normals), normals)
        gl.glBufferSubData(gl.GL_ARRAY_BUFFER, len(vertices) + len(normals), len(indexiesInMatrix), indexiesInMatrix)

        vbo.release()

        return numberOfLandscapeVertices

    def buildMeshTriangles(self, matrix, z0):
        ''' Generates lists of vertices, normals and vertices' indexes
            of mesh (landscape or water).
        '''

#       Used for landscape mesh, when z0 is always constant 0
        def constant(value):
            return lambda *args: value


        triangles = []
        n = self.logicalResources.n
        m = self.logicalResources.m

        if not callable(z0):
            triangles.extend([
                     0,      0, z0, (0, 0, -1), (-1, -1),
                     0,      n, z0, (0, 0, -1), (-1, -1),
                     m,      0, z0, (0, 0, -1), (-1, -1),

                     m,      0, z0, (0, 0, -1), (-1, -1),
                     0,      n, z0, (0, 0, -1), (-1, -1),
                     m,      n, z0, (0, 0, -1), (-1, -1),
                ])
            z0 = constant(z0)

        for i in range(     n):
            for j in range(     m):
                p = (j, i)
#       up        
                z1 = z0(i-1, j) + (0 if i == 0 else matrix[i-1][j])
#       right        
                z2 = z0(i, j+1) + (0 if j+1 == m else matrix[i][j+1])
#       down
                z3 = z0(i+1, j) + (0 if i+1 == n else matrix[i+1][j])
#       left
                z4 = z0(i, j-1) + (0 if j == 0 else matrix[i][j-1])
                z05 = z0(i, j)
                z5 = z05 + matrix[i][j]

                triangles.extend([
                        j,     i, z5, (0, 0, 1), p,
                    j + 1,     i, z5, (0, 0, 1), p,
                    j + 1, i + 1, z5, (0, 0, 1), p,
                   
                        j,     i, z5, (0, 0, 1), p,
                    j + 1, i + 1, z5, (0, 0, 1), p,
                        j, i + 1, z5, (0, 0, 1), p,
                    ])
                

                if z4 < z5:
                    z4 = max(z05, z4)
                    triangles.extend([
                            j,     i, z5, (-1, 0, 0), p,
                            j, i + 1, z5, (-1, 0, 0), p,
                            j, i + 1, z4, (-1, 0, 0), p,
                           
                            j,     i, z5, (-1, 0, 0), p,
                            j, i + 1, z4, (-1, 0, 0), p,
                            j,     i, z4, (-1, 0, 0), p,
                        ])

                if z2 < z5:
                    z2 = max(z05, z2)
                    triangles.extend([
                        j + 1, i + 1, z5, ( 1, 0, 0), p,
                        j + 1,     i, z5, ( 1, 0, 0), p,
                        j + 1,     i, z2, ( 1, 0, 0), p,
                       
                        j + 1, i + 1, z5, ( 1, 0, 0), p,
                        j + 1,     i, z2, ( 1, 0, 0), p,
                        j + 1, i + 1, z2, ( 1, 0, 0), p,
                        ])

                if z1 < z5:
                    z1 = max(z05, z1)
                    triangles.extend([
                        j + 1,     i, z5, (0, -1, 0), p,
                            j,     i, z5, (0, -1, 0), p,
                            j,     i, z1, (0, -1, 0), p,
                   
                        j + 1,     i, z5, (0, -1, 0), p,
                            j,     i, z1, (0, -1, 0), p,
                        j + 1,     i, z1, (0, -1, 0), p,
                        ])

                if z3 < z5:
                    z3 = max(z05, z3)
                    triangles.extend([
                            j, i + 1, z5, (0,  1, 0), p,
                        j + 1, i + 1, z5, (0,  1, 0), p,
                        j + 1, i + 1, z3, (0,  1, 0), p,
                       
                            j, i + 1, z5, (0,  1, 0), p,
                        j + 1, i + 1, z3, (0,  1, 0), p,
                            j, i + 1, z3, (0,  1, 0), p,
                        ])

        vertices = []
        normals = []
        indexiesInMatrix = []
        i = 0
        while i < len(triangles):
            vertices.extend([
                triangles[i]/m, 
                triangles[i+1]/n, 
                triangles[i+2] * ZScale])
            normals.extend(triangles[i+3])
            indexiesInMatrix.extend([
                triangles[i+4][0],
                triangles[i+4][1],
                ])
            i += 5

        return vertices, normals, indexiesInMatrix

