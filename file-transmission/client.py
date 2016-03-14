#!/usr/bin/env python3 

import socket
import sys

if len(sys.argv) < 2:
    print('Specify file.', file=sys.stderr)
    exit(1)

SERVER_HOST = '4.8.15.16'
#SERVER_HOST = 'localhost'
SERVER_PORT = 13337
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect((SERVER_HOST, SERVER_PORT))

with open(sys.argv[1], 'rb') as f:
    data = f.read(2**22)
    while len(data) > 0:
        s.sendall(data)
        data = f.read(2**22)

s.close()

