import array
from itertools import repeat, islice, chain
import math
import random
import os.path


from PyQt5.QtCore import QPoint
from PyQt5.QtGui import QMatrix4x4, QVector3D


""" Keeps logical scene resources (landscape, water, coordinates)"""
class Resources(object):
    directory = os.path.dirname(__file__)

    def __init__(self):

        self.center = QVector3D(0.5, 0.5, 0.5)    
        self.eye = QVector3D(0, 0, 3)

        lookDirection = (self.center - self.eye).normalized()
        self.up = QVector3D(0, 0, 1)
        self.up = (self.up - lookDirection * QVector3D.dotProduct(lookDirection, self.up)).normalized()
    
        self.clearColor = [0.3, 0.25, 0.35, 1.0]
        self.selectedLandscapeCell = (float('inf'), float('inf'))

        self.lightSourcePosition = QVector3D(1.5, 1.5, 2.7)
        self.lastMousePosition = QPoint()

        try:
            self.n, self.m, self.landscapeHeightsMatrix = self.loadLandscapeHeightsMatrix()
        except:
            self.n, self.m = 10, 10
            self.landscapeHeightsMatrix = self.generateLandscapeHeightsMatrix()
        self.generateWaterHeightsMatrix()

    def changeLandscapeHeight(self, i, j, dh):
        ''' Changes the landscape at (i, j) for dh '''
        v = self.landscapeHeightsMatrix[i][j]
        self.landscapeHeightsMatrix[i][j] = max(0, min(40, v + dh))

        maxLandscapeHeight = max(map(max, self.landscapeHeightsMatrix))
        self.changeWaterHeight(i, j, maxLandscapeHeight - self.landscapeHeightsMatrix[i][j])

    def changeWaterHeight(self, i, j, newHeight):
        ''' Sets water height at (i, j) to newHeight '''
        self.waterHeightsMatrix[i][j] = newHeight


    def expandLandscapeHeightsMatrix(self, dn, dm):
        ''' Changes landscape size for dn columns and dm rows '''
        if self.n + dn < 1 or self.n + dn > 30: return
        if self.m + dm < 1 or self.m + dm > 30: return
        self.n += dn
        landscapeHeightsMatrix = list(islice(chain(self.landscapeHeightsMatrix, repeat([0]*self.m)), self.n))
        self.m += dm
        for i in range(self.n):
            landscapeHeightsMatrix[i] = list(islice(chain(landscapeHeightsMatrix[i], repeat(0)), self.m))

        self.landscapeHeightsMatrix = landscapeHeightsMatrix
        self.generateWaterHeightsMatrix()

    def loadLandscapeHeightsMatrix(self):
        ''' Loads landscape matrix from file '''
        landscapeHeightsMatrix = []
        with open('{}/matrix.csv'.format(Resources.directory), 'r') as f:
            for line in f:
                if len(line.strip()) == 0: continue
                landscapeHeightsMatrix.append(list(map(int, line.strip().split(','))))
                assert len(landscapeHeightsMatrix[len(landscapeHeightsMatrix) - 1]) == len(landscapeHeightsMatrix[0])

        return len(landscapeHeightsMatrix), len(landscapeHeightsMatrix[0]), landscapeHeightsMatrix

    def saveLandscapeHeightsMatrix(self):
        ''' Saves landscape matrix to file '''
        with open('matrix.csv', 'w') as f:
            for row in self.landscapeHeightsMatrix:
                print(*row, sep=',', file=f)
        
    
    def generateWaterHeightsMatrix(self):
        ''' Generates water heights matrix from current landscape heights '''
        maxLandscapeHeight = max(map(max, self.landscapeHeightsMatrix))
        self.waterHeightsMatrix = []
        for i in range(self.n):
            self.waterHeightsMatrix.append([])
            for j in range(self.m):
                self.waterHeightsMatrix[i].append(maxLandscapeHeight - self.landscapeHeightsMatrix[i][j])

        return self.waterHeightsMatrix

    def generateLandscapeHeightsMatrix(self, minLandscapeHeight=0, maxLandscapeHeight=40):
        ''' Generates random landscape '''
        landscapeHeightsMatrix = []
        for i in range(self.n):
            landscapeHeightsMatrix.append([])
            for j in range(self.m):
                landscapeHeightsMatrix[len(landscapeHeightsMatrix) - 1].append(
                            random.randint(minLandscapeHeight, maxLandscapeHeight))

        return landscapeHeightsMatrix

    def rotateUpDown(self, angle):
        ''' Rotates camera up/down '''
        axis = QVector3D.crossProduct(self.up, self.center - self.eye)

        transform = QMatrix4x4()
        transform.translate(self.center)
        transform.rotate(angle, axis)
        transform.translate(-self.center)

        self.eye = transform.map(self.eye)

        transform = QMatrix4x4()
        transform.rotate(angle, axis)
        self.up = transform.map(self.up)

    def rotateLeftRight(self, angle):
        ''' Rotates camera left/right '''
        transform = QMatrix4x4()
        transform.translate(self.center)
        transform.rotate(angle, QVector3D(0, 0, 1))
        transform.translate(-self.center)

        self.eye = transform.map(self.eye)

        transform = QMatrix4x4()
        transform.rotate(angle, QVector3D(0, 0, 1))
        self.up = transform.map(self.up)

    def moveForwardBackward(self, magnitude):
        ''' Zoom camera in/out (logically - move along look direction) '''
        direction = self.center - self.eye

        self.eye += magnitude * direction

    def mvmatrix(self, width=None, height=None):
        ''' Computes MV or MVP matrix '''
        mvpmatrix = QMatrix4x4()
        if width and height: mvpmatrix.perspective(45, width/height, 0.5, 6.0)
        
        mvpmatrix.lookAt(self.eye, self.center, self.up)

        return mvpmatrix
