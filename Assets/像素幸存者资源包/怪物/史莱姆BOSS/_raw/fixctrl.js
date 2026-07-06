// Rewrites SlimeBoss.controller with unique BigInt state fileIDs (fixes duplicate-id bug).
const fs = require('fs');
const path = require('path');
const ANIMDIR = path.resolve(__dirname, '..', '..', '..', '..', '怪物prefab', 'SlimeBoss', 'animation');

const A = {
  Walk:'274ef9a9767fbce95adb1db5fe967efa', Devour:'f30335e15338a0cbe2a99e02a456b9cc',
  SwordCast:'7b22ad568874caeb0494d4633560b78a', BowCast:'b203bd4f42f2ef3a4e3eae83c2e1a865',
  Transform:'b28f9fee3d92ee383dc302726975c42c', DragonWalk:'9db824230a9434ae5886221cc81f6ff8',
  Death:'3821b2a4c7bfe89ba2f718ee46400e1c',
};
const order = ['Walk','Devour','SwordCast','BowCast','Transform','DragonWalk','Death'];

const SM = '1107000000000000001';
const baseId = 1102000000000000010n; // BigInt so +i keeps full precision

let childStates = '';
let stateObjs = '';
order.forEach((name, i) => {
  const id = (baseId + BigInt(i)).toString();
  childStates += `  - serializedVersion: 1\n    m_State: {fileID: ${id}}\n    m_Position: {x: 260, y: ${60 + i * 70}, z: 0}\n`;
  stateObjs += `--- !u!1102 &${id}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: ${name}
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {x: 50, y: 50, z: 0}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {fileID: 7400000, guid: ${A[name]}, type: 2}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
`;
});

const defaultId = baseId.toString();
const out = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: SlimeBoss
  serializedVersion: 5
  m_AnimatorParameters: []
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {fileID: ${SM}}
    m_Mask: {fileID: 0}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {fileID: 9100000}
--- !u!1107 &${SM}
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Base Layer
  m_ChildStates:
${childStates}  m_ChildStateMachines: []
  m_AnyStateTransitions: []
  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {x: 50, y: 20, z: 0}
  m_EntryPosition: {x: 50, y: 120, z: 0}
  m_ExitPosition: {x: 800, y: 120, z: 0}
  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}
  m_DefaultState: {fileID: ${defaultId}}
${stateObjs}`;

fs.writeFileSync(path.join(ANIMDIR, 'SlimeBoss.controller'), out);
console.log('controller rewritten. state ids:');
order.forEach((n, i) => console.log('  ' + n + ' = ' + (baseId + BigInt(i)).toString()));
