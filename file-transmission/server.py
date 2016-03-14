#!/usr/bin/env python3 

import socket
import sys

#SERVER_HOST = 'localhost'
SERVER_HOST = '4.8.15.16'
SERVER_PORT = 13337
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.bind((SERVER_HOST, SERVER_PORT))
file_no = 0
while True:
    s.listen(1)
    conn, addr = s.accept()
    print('Connected by', addr)
    with open('{}.raw'.format(file_no), 'wb') as f:
        data = conn.recv(4096)
        while len(data) > 0:
            f.write(data)
            data = conn.recv(4096)
    conn.close()
    file_no += 1

