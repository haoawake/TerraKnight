"""Decode common PlayMaker ActionData parameters; preserve unresolved payloads explicitly."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'artifacts/enemy-attack-audit'
TYPE_NAMES = 'Integer Boolean Float String Color ObjectReference LayerMask Enum Vector2 Vector3 Vector4 Rect Array Character AnimationCurve FsmFloat FsmInt FsmBool FsmString FsmGameObject FsmOwnerDefault FunctionCall FsmAnimationCurve FsmEvent FsmObject FsmColor Unsupported GameObject FsmVector3 LayoutOption FsmRect FsmEventTarget FsmMaterial FsmTexture Quaternion FsmQuaternion FsmProperty FsmVector2 FsmTemplateControl FsmVar CustomClass FsmArray FsmEnum'.split()
LISTS = {5:'unityObjectParams', 18:'fsmStringParams',19:'fsmGameObjectParams',20:'fsmOwnerDefaultParams',21:'functionCallParams',22:'animationCurveParams',24:'fsmObjectParams',29:'layoutOptionParams',31:'fsmEventTargetParams',32:'fsmObjectParams',33:'fsmObjectParams',36:'fsmPropertyParams',38:'fsmTemplateControlParams',39:'fsmVarParams',41:'fsmArrayParams',42:'fsmEnumParams'}
VARS = {15:('f', 'floatVariables'),16:('i','intVariables'),17:('?', 'boolVariables'),25:('4f','colorVariables'),28:('3f','vector3Variables'),30:('4f','rectVariables'),35:('4f','quaternionVariables'),37:('2f','vector2Variables')}
FRESH_LISTS = {15:'fsmFloatParams',16:'fsmIntParams',17:'fsmBoolParams',18:'fsmStringParams',25:'fsmColorParams',28:'fsmVector3Params',30:'fsmRectParams',35:'fsmQuaternionParams',37:'fsmVector2Params'}


def simplify(value):
    if isinstance(value, dict):
        return {k:simplify(v) for k,v in value.items() if k not in ('tooltip','showInInspector','networkSync')}
    if isinstance(value, list):
        return [simplify(x) for x in value]
    return value


def param(data, fsm, i):
    typ, pos, size = data['paramDataType'][i], data['paramDataPos'][i], data['paramByteDataSize'][i]
    raw = data['_rawBytes']
    version = fsm['dataVersion']
    if typ in LISTS:
        return simplify(data[LISTS[typ]][pos])
    if version > 1 and typ in FRESH_LISTS:
        return simplify(data[FRESH_LISTS[typ]][pos])
    if typ in VARS:
        fmt, pool = VARS[typ]
        length = struct.calcsize('<' + fmt)
        if size < length + 1 or pos + size > len(raw):
            raise ValueError('Invalid named parameter bounds')
        initial = struct.unpack_from('<' + fmt, raw, pos)
        name = raw[pos+length+1:pos+size].decode('utf-8')
        value = dict(serializedValue=initial[0] if len(initial)==1 else list(initial), useVariable=bool(raw[pos+length]))
        if name:
            value['variable'] = name
            v = next((v for v in fsm['variables'][pool] if v['name'] == name), None)
            value['fsmInitialValue'] = simplify(v.get('value')) if v else None
            value['runtimeValueUnknown'] = True
        return value
    if typ in (3,23):
        return data['stringParams'][pos] if version > 1 and data['stringParams'] else raw[pos:pos+size].decode('utf-8')
    if typ in (0,1,2,6,7,8,9,10,11,34):
        fmt = {0:'i',1:'?',2:'f',6:'i',7:'i',8:'2f',9:'3f',10:'4f',11:'4f',34:'4f'}[typ]
        values = struct.unpack_from('<'+fmt,raw,pos)
        return values[0] if len(values)==1 else list(values)
    return dict(unresolved=True, dataPosition=pos, byteSize=size, rawHex=raw[pos:pos+size].hex())


def actions(fsm, state):
    d = state['actionData']
    d['_rawBytes'] = bytes(d['byteData'])
    out = []
    for i, name in enumerate(d['actionNames']):
        start = d['actionStartIndex'][i]
        end = d['actionStartIndex'][i+1] if i+1 < len(d['actionNames']) else len(d['paramName'])
        params = []
        for j in range(start, end):
            try:
                value = param(d,fsm,j)
            except Exception as exc:
                value = dict(unresolved=True,error=str(exc),dataPosition=d['paramDataPos'][j],byteSize=d['paramByteDataSize'][j])
            params.append(dict(name=d['paramName'][j],type=TYPE_NAMES[d['paramDataType'][j]],value=value))
        out.append(dict(type=name,enabled=bool(d['actionEnabled'][i]) if i<len(d['actionEnabled']) else True,parameters=params))
    return out


def main():
    native = json.loads((DEST/'decoded-index.json').read_text(encoding='utf-8'))
    target = DEST/'actions'
    target.mkdir(exist_ok=True)
    index, counts = [], Counter()
    for scene in native['scenes']:
        full = json.loads((DEST/scene['file']).read_text(encoding='utf-8'))
        rows = []
        for row in full['decoded']:
            fsm = row['value']['fsm']
            states = []
            for s in fsm['states']:
                aa = actions(fsm,s)
                counts['actions'] += len(aa)
                counts['disabledActions'] += sum(not a['enabled'] for a in aa)
                counts['parameters'] += sum(len(a['parameters']) for a in aa)
                counts['unresolvedParameters'] += sum(isinstance(p['value'],dict) and p['value'].get('unresolved',False) for a in aa for p in a['parameters'])
                counts['states'] += 1
                counts['transitions'] += len(s['transitions'])
                states.append(dict(name=s['name'],transitions=[dict(event=t['fsmEvent']['name'],to=t['toState']) for t in s['transitions']],actions=aa))
            rows.append(dict(owner=row['owner'],ownerPathId=row['ownerPathId'],pathId=row['pathId'],name=fsm['name'],startState=fsm['startState'],globalTransitions=[dict(event=t['fsmEvent']['name'],to=t['toState']) for t in fsm['globalTransitions']],variables=simplify(fsm['variables']),states=states))
        path='actions/'+Path(scene['file']).name
        (DEST/path).write_text(json.dumps(dict(scene=scene['scene'],fsms=rows),ensure_ascii=False,separators=(',',':')),encoding='utf-8')
        index.append(dict(scene=scene['scene'],file=path,fsms=len(rows)))
    summary=dict(counts=counts,scenes=index,notes='Action enabled flags preserved. Variable initial values are not runtime values. Unresolved parameters are retained in decoded raw ActionData. Cross-FSM events, code, prefab instances and all possible paths still need tracing.')
    (DEST/'action-index.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(counts,indent=2))


if __name__=='__main__':
    main()
