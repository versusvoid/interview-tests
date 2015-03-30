#!/usr/bin/env python

import numpy as np
import itertools
import time

class Solver(object):

    """ Matrix with landscape heights. """
    matrix = None

    """ Matrix with water heights. """
    heights = None

    def __init__(self, matrix):
        self.matrix = np.array(matrix)
        maxLandscapeHeight = np.max(self.matrix) 
        self.heights = maxLandscapeHeight * np.ones_like(self.matrix) - self.matrix

    def surroundings(self, p):
        dimensions = self.matrix.shape
        result = []
        p_ = list(p)
        for i in range(len(dimensions)):
            if p[i] - 1 >= 0:
                p_[i] = p[i] - 1
                yield(tuple(p_))
                p_[i] = p[i]
            if p[i] + 1 < dimensions[i]:
                p_[i] = p[i] + 1
                yield(tuple(p_))
                p_[i] = p[i]


    def border(self, i):
        dimensions = self.matrix.shape
        def range_for(j):
            if j == i and len(dimensions) > 1: return range(dimensions[i])
            else: return [0, dimensions[j] - 1]
        return itertools.product(*[range_for(j) for j in range(len(dimensions))])

    def exclude_borders(self):
        borders = itertools.chain.from_iterable([self.border(i) for i in range(len(self.matrix.shape))])

        for p in borders:
            if self.heights[p] == 0: continue

            stack = [p]
            while len(stack) > 0:
                p = stack.pop()
                yield ('Select', p)
                self.heights[p] = 0
                yield ('Zero', p)

                for p_ in self.surroundings(p):
                    if self.heights[p_] != 0 and self.matrix[p_] >= self.matrix[p]:
                        stack.append(p_)
                        yield ('Expand over', p_)

    def is_border(self, p):
        for p_ in self.surroundings(p):
            if self.heights[p_] > 0 and self.matrix[p_] + self.heights[p_] > self.matrix[p]: return True

        return False

    def find_minimal_border(self):
        min_value = None
        min_position = None
        for p in itertools.product(*[range(n) for n in self.matrix.shape]):
            if self.heights[p] > 0: continue
            if self.is_border(p):
                if min_value is None or min_value > self.matrix[p]:
                    min_value = self.matrix[p]
                    min_position = p

        return min_position

    def expand_minimal_border(self, min_border):
        min_value = self.matrix[min_border]

        stack = [min_border]
        while len(stack) > 0:
            p = stack.pop()
            yield ('Select', p)
            if self.matrix[p] == min_value:
                self.heights[p] = 0
                yield ('Zero', p)
            else:
                self.heights[p] = min_value - self.matrix[p]
                yield ('Lower height', p, self.heights[p])

            
            for p_ in self.surroundings(p):
                if self.matrix[p_] > min_value:
                    self.heights[p_] = 0
                    yield ('Zero', p_)
                elif self.matrix[p_] + self.heights[p_] > min_value:
                    stack.append(p_)
                    yield ('Expand over', p_)

    def compute(self):

        yield from self.exclude_borders()

        min_border = self.find_minimal_border()
        while min_border is not None:
            yield ('Next minimal border', min_border)
            yield from self.expand_minimal_border(min_border)
            min_border = self.find_minimal_border()
