"""Reject-only local handshake probes: never joins the world or sends player state."""
import argparse
import io
import json
import socket
import struct

def string(s):
    b=s.encode('utf-8'); n=len(b); prefix=bytearray()
    while n>=128: prefix.append((n&127)|128); n>>=7
    prefix.append(n); return bytes(prefix)+b

def readstring(f):
    n=0; shift=0
    while True:
        b=f.read(1)[0]; n|=(b&127)<<shift
        if not b&128: break
        shift+=7
    return f.read(n).decode('utf-8')

def nettext(f):
    mode=f.read(1)[0]; s=readstring(f)
    return [s]+[x for _ in range(f.read(1)[0] if mode else 0) for x in nettext(f)]

def exact(s,n):
    b=b''
    while len(b)<n:
        part=s.recv(n-len(b))
        if not part: raise RuntimeError('Disconnected before complete response')
        b+=part
    return b

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--port',type=int,required=True);p.add_argument('--output',required=True);a=p.parse_args()
    rows=[]
    for version in ('TerraKnight4.4.2/net13','TerraKnight4.4.1/net14'):
        with socket.create_connection(('127.0.0.1',a.port),timeout=10) as s:
            body=bytes([1])+string(version)+string('')
            s.sendall(struct.pack('<H',len(body)+2)+body)
            size=struct.unpack('<H',exact(s,2))[0];response=exact(s,size-2)
            if response[0]!=2: raise RuntimeError('Incompatible client was not kicked')
            text=nettext(io.BytesIO(response[1:]))
            passed='TerraKnight4.4.2/net14' in text and version in text and 'LegacyMultiplayer.4' in text
            rows.append(dict(Version=version,Kicked=True,Reason=text,Passed=passed))
    from pathlib import Path
    result=dict(Completed=True,Failures=[r for r in rows if not r['Passed']],Checks=rows)
    Path(a.output).write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))
    raise SystemExit(bool(result['Failures']))
