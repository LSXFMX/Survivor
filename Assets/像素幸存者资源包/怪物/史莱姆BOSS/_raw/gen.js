// Generates all Unity assets for the Slime Boss: sprite .meta, .anim + .meta,
// .controller + .meta, prefabs + .meta, script .meta.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', '..', '..', '..'); // -> Assets/
// __dirname = Assets/像素幸存者资源包/怪物/史莱姆BOSS/_raw
const ASSETS = path.resolve(__dirname, '..', '..', '..', '..'); // Assets
const SPRDIR = path.resolve(__dirname, '..');                    // 史莱姆BOSS
const PREFABDIR = path.join(ASSETS, '怪物prefab', 'SlimeBoss');
const ANIMDIR = path.join(PREFABDIR, 'animation');
const CSDIR = path.join(ASSETS, 'C#');
fs.mkdirSync(ANIMDIR, { recursive: true });

const G = {
  walk1:'160c58c090ce6982d849eb2a4d98dcc1', walk2:'c75ef188e13d0609b0a225c5821a6cd2',
  walk3:'0d2eceb61c3fb93713daf47c997ac0e5', walk4:'578e6433a6c7c09e3179e4fd4ce9f2c9',
  dev1:'b2beb7ab09f7f06e53d956b6f410a3ed',  dev2:'443e54d2f54a486a4b2afb260a619341',
  tr1:'b9af1c78c94e75ec5b8e8f915bb376a5',   tr2:'384f976717587e1113db77183e6aa7d7',
  tr3:'a0e886d69b18e4d75a759df50d3bb3d4',   tr4:'9319d0afa569bfcf26a9fd2477f3d0af',
  dr1:'26bec38d5e81cafa95adfa39616483cd',   dr2:'1d0deffcba1781ce7546b572c26a5078',
  sword:'2822cc87a9c37bddb1ba0ffebe618eef', arrow:'4092d98ac79da819362bf08f11761011',
  a_walk:'274ef9a9767fbce95adb1db5fe967efa', a_devour:'f30335e15338a0cbe2a99e02a456b9cc',
  a_swordcast:'7b22ad568874caeb0494d4633560b78a', a_bowcast:'b203bd4f42f2ef3a4e3eae83c2e1a865',
  a_transform:'b28f9fee3d92ee383dc302726975c42c', a_dragonwalk:'9db824230a9434ae5886221cc81f6ff8',
  a_death:'3821b2a4c7bfe89ba2f718ee46400e1c',
  ctrl:'f93173e4aee789ad4d838f00756f411d',
  pf_boss:'adf2b732a3b383d9f60a36ef7d9c9775', pf_sword:'7307d5ded26b17a516263374ad07d2ce',
  pf_arrow:'3de45f5a4bcb6971ac4a4530fb73d96e',
  sc_boss:'4faad8b42e3d7d1857cc3d13f1c52441', sc_proj:'af24e457aa4e9f2aaa18a9813786d537',
};

// shared refs (borrowed from existing enemy prefabs)
const MAT   = '{fileID: -876546973899608171, guid: 8bef114c142cd4743948e426f1da55a1, type: 3}';
const REDMAT= '{fileID: -876546973899608171, guid: 006fb32767b8c5341bff9e7812b46530, type: 3}';
const ATKNUM= '{fileID: 6161263895538339157, guid: fa76286ee73de29449ad1516333066ba, type: 3}';
const EXP   = '{fileID: 7733258394847694891, guid: 2a634f41aed7e5a43bc79c0cd73c4360, type: 3}';
const SWORD_ROOT = '7300000000000000001';
const ARROW_ROOT = '7300000000000000002';

// ---------- sprite .meta ----------
function spriteMeta(guid, pivotY) {
  const alignment = pivotY === 0.5 ? 0 : 9;
  return `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: ${alignment}
  spritePivot: {x: 0.5, y: ${pivotY}}
  spritePixelsToUnits: 300
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: ${guid}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

// ---------- .anim ----------
function animClip(name, frames, loop, stop) {
  const curve = frames.map(f => `    - time: ${f.t}\n      value: {fileID: 21300000, guid: ${f.g}, type: 3}`).join('\n');
  const mapping = frames.map(f => `    - {fileID: 21300000, guid: ${f.g}, type: 3}`).join('\n');
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: ${name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
${curve}
    attribute: m_Sprite
    path: 
    classID: 212
    script: {fileID: 0}
    flags: 2
  m_SampleRate: 60
  m_WrapMode: 0
  m_Bounds:
    m_Center: {x: 0, y: 0, z: 0}
    m_Extent: {x: 0, y: 0, z: 0}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {fileID: 0}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
${mapping}
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {fileID: 0}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: ${stop}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: ${loop}
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
`;
}
function nativeMeta(guid, mainId) {
  return `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: ${mainId}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}
function prefabMeta(guid) {
  return `fileFormatVersion: 2
guid: ${guid}
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}
function scriptMeta(guid) {
  return `fileFormatVersion: 2
guid: ${guid}
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

// ---------- controller ----------
function controller() {
  const states = [
    ['Walk', G.a_walk], ['Devour', G.a_devour], ['SwordCast', G.a_swordcast],
    ['BowCast', G.a_bowcast], ['Transform', G.a_transform], ['DragonWalk', G.a_dragonwalk],
    ['Death', G.a_death],
  ];
  const baseId = 1200000000000000002;
  let childStates = '';
  let stateObjs = '';
  states.forEach((s, i) => {
    const id = baseId + i;
    childStates += `  - serializedVersion: 1\n    m_State: {fileID: ${id}}\n    m_Position: {x: 260, y: ${60 + i * 70}, z: 0}\n`;
    stateObjs += `--- !u!1102 &${id}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: ${s[0]}
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
  m_Motion: {fileID: 7400000, guid: ${s[1]}, type: 2}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
`;
  });
  return `%YAML 1.1
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
    m_StateMachine: {fileID: 1107000000000000001}
    m_Mask: {fileID: 0}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {fileID: 9100000}
--- !u!1107 &1107000000000000001
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
  m_DefaultState: {fileID: ${baseId}}
${stateObjs}`;
}

// ---------- boss prefab ----------
function bossPrefab() {
  const GO=3331652924953612050n, TR=5897644654296172774n, SR=3168974520617928507n,
        AN=5754145641337289948n, MB=2444078356638682249n, RB=8713925415675215983n,
        C1=4779867028028055655n, C2=4609244634355564219n;
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &${GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: ${TR}}
  - component: {fileID: ${SR}}
  - component: {fileID: ${AN}}
  - component: {fileID: ${MB}}
  - component: {fileID: ${RB}}
  - component: {fileID: ${C1}}
  - component: {fileID: ${C2}}
  m_Layer: 6
  m_Name: SlimeBoss
  m_TagString: enemy
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  serializedVersion: 2
  m_LocalRotation: {x: 0.38268343, y: 0, z: 0, w: 0.92387956}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1.6, y: 1.6, z: 1.6}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 45, y: 0, z: 0}
--- !u!212 &${SR}
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - ${MAT}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 1
  m_Sprite: {fileID: 21300000, guid: ${G.walk1}, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 1, y: 1}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
--- !u!95 &${AN}
Animator:
  serializedVersion: 5
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Enabled: 1
  m_Avatar: {fileID: 0}
  m_Controller: {fileID: 9100000, guid: ${G.ctrl}, type: 2}
  m_CullingMode: 0
  m_UpdateMode: 0
  m_ApplyRootMotion: 0
  m_LinearVelocityBlending: 0
  m_StabilizeFeet: 0
  m_WarningMessage: 
  m_HasTransformHierarchy: 1
  m_AllowConstantClipSamplingOptimization: 1
  m_KeepAnimatorStateOnDisable: 0
  m_WriteDefaultValuesOnDisable: 0
--- !u!114 &${MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${G.sc_boss}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  rolename: "\\u53F2\\u83B1\\u59C6BOSS"
  health: 500
  healthmax: 500
  atk: 50
  def: 0
  speed: 3
  CR: 0
  CD: 0
  EVA: 0
  DR: 0
  regen: 0
  exp: 0
  expmax: 0
  level: 0
  atknumber: ${ATKNUM}
  rolestate: 0
  role: {fileID: 0}
  Sca: 1.6
  material: ${MAT}
  red: ${REDMAT}
  expstone: ${EXP}
  bossScale: 1.6
  dragonScale: 2.4
  slimeSpeed: 3
  dragonSpeed: 6
  mergeInterval: 6
  mergeRadius: 9
  mergeHealthPct: 0.05
  mergeAtkPct: 0.05
  swordUnlockCount: 5
  swordCd: 10
  swordCount: 5
  swordSpread: 60
  swordSpeed: 14
  swordLifetime: 3.5
  swordDamageMul: 1
  bowUnlockCount: 10
  bowCd: 10
  arrowCount: 9
  arrowSpread: 90
  arrowSpeed: 20
  arrowLifetime: 3.5
  arrowDamageMul: 0.7
  dragonHpThreshold: 0.1
  dragonHealPct: 0.5
  dragonAuraRadius: 9
  dragonAuraPctPerSec: 0.04
  dragonContactMul: 1.5
  swordPrefab: {fileID: ${SWORD_ROOT}, guid: ${G.pf_sword}, type: 3}
  arrowPrefab: {fileID: ${ARROW_ROOT}, guid: ${G.pf_arrow}, type: 3}
--- !u!54 &${RB}
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  serializedVersion: 4
  m_Mass: 10
  m_Drag: 0
  m_AngularDrag: 0.05
  m_CenterOfMass: {x: 0, y: 0, z: 0}
  m_InertiaTensor: {x: 1, y: 1, z: 1}
  m_InertiaRotation: {x: 0, y: 0, z: 0, w: 1}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ImplicitCom: 1
  m_ImplicitTensor: 1
  m_UseGravity: 0
  m_IsKinematic: 1
  m_Interpolate: 0
  m_Constraints: 0
  m_CollisionDetection: 0
--- !u!136 &${C1}
CapsuleCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 0
  serializedVersion: 2
  m_Radius: 0.5
  m_Height: 1.2
  m_Direction: 1
  m_Center: {x: 0, y: 0.5, z: 0}
--- !u!136 &${C2}
CapsuleCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 2
  m_Radius: 0.5
  m_Height: 1.2
  m_Direction: 1
  m_Center: {x: 0, y: 0.5, z: 0}
`;
}

// ---------- projectile prefab ----------
function projPrefab(rootId, spriteGuid, name, damage, speed, hitRadius) {
  const GO = BigInt(rootId);
  const TR = GO + 10n, SR = GO + 20n, MB = GO + 30n;
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &${GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: ${TR}}
  - component: {fileID: ${SR}}
  - component: {fileID: ${MB}}
  m_Layer: 0
  m_Name: ${name}
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  serializedVersion: 2
  m_LocalRotation: {x: 0.38268343, y: 0, z: 0, w: 0.92387956}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 0.6, y: 0.6, z: 0.6}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 45, y: 0, z: 0}
--- !u!212 &${SR}
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - ${MAT}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 2
  m_Sprite: {fileID: 21300000, guid: ${spriteGuid}, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 1, y: 1}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
--- !u!114 &${MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${GO}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${G.sc_proj}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  speed: ${speed}
  damage: ${damage}
  lifetime: 3.5
  pass: 0
  hitRadius: ${hitRadius}
`;
}

// ===== write everything =====
const w = (p, s) => { fs.writeFileSync(p, s); console.log('  ' + path.relative(ASSETS, p)); };

// sprite metas (bottom pivot for body/dragon/transform; center for projectiles)
const S = (name) => path.join(SPRDIR, name + '.png.meta');
w(S('SlimeBossWalk1'), spriteMeta(G.walk1, 0));
w(S('SlimeBossWalk2'), spriteMeta(G.walk2, 0));
w(S('SlimeBossWalk3'), spriteMeta(G.walk3, 0));
w(S('SlimeBossWalk4'), spriteMeta(G.walk4, 0));
w(S('SlimeBossDevour1'), spriteMeta(G.dev1, 0));
w(S('SlimeBossDevour2'), spriteMeta(G.dev2, 0));
w(S('SlimeBossTransform1'), spriteMeta(G.tr1, 0));
w(S('SlimeBossTransform2'), spriteMeta(G.tr2, 0));
w(S('SlimeBossTransform3'), spriteMeta(G.tr3, 0));
w(S('SlimeBossTransform4'), spriteMeta(G.tr4, 0));
w(S('SlimeDragonWalk1'), spriteMeta(G.dr1, 0));
w(S('SlimeDragonWalk2'), spriteMeta(G.dr2, 0));
w(S('SlimeSword'), spriteMeta(G.sword, 0.5));
w(S('SlimeArrow'), spriteMeta(G.arrow, 0.5));

// anim clips + metas
const A = (name) => path.join(ANIMDIR, name + '.anim');
w(A('Walk'), animClip('Walk', [{t:0,g:G.walk1},{t:0.15,g:G.walk2},{t:0.3,g:G.walk3},{t:0.45,g:G.walk4}], 1, 0.6));
w(A('Walk')+'.meta', nativeMeta(G.a_walk, 7400000));
w(A('Devour'), animClip('Devour', [{t:0,g:G.dev1},{t:0.2,g:G.dev2}], 1, 0.4));
w(A('Devour')+'.meta', nativeMeta(G.a_devour, 7400000));
w(A('SwordCast'), animClip('SwordCast', [{t:0,g:G.dev2},{t:0.2,g:G.dev1}], 1, 0.4));
w(A('SwordCast')+'.meta', nativeMeta(G.a_swordcast, 7400000));
w(A('BowCast'), animClip('BowCast', [{t:0,g:G.dev1},{t:0.15,g:G.dev2}], 1, 0.3));
w(A('BowCast')+'.meta', nativeMeta(G.a_bowcast, 7400000));
w(A('Transform'), animClip('Transform', [{t:0,g:G.tr1},{t:0.35,g:G.tr2},{t:0.7,g:G.tr3},{t:1.05,g:G.tr4}], 0, 1.4));
w(A('Transform')+'.meta', nativeMeta(G.a_transform, 7400000));
w(A('DragonWalk'), animClip('DragonWalk', [{t:0,g:G.dr1},{t:0.25,g:G.dr2}], 1, 0.5));
w(A('DragonWalk')+'.meta', nativeMeta(G.a_dragonwalk, 7400000));
w(A('Death'), animClip('Death', [{t:0,g:G.dev2},{t:0.3,g:G.walk4}], 0, 0.6));
w(A('Death')+'.meta', nativeMeta(G.a_death, 7400000));

// controller + meta
w(path.join(ANIMDIR, 'SlimeBoss.controller'), controller());
w(path.join(ANIMDIR, 'SlimeBoss.controller.meta'), nativeMeta(G.ctrl, 9100000));

// prefabs + metas
w(path.join(PREFABDIR, 'SlimeBoss.prefab'), bossPrefab());
w(path.join(PREFABDIR, 'SlimeBoss.prefab.meta'), prefabMeta(G.pf_boss));
w(path.join(PREFABDIR, 'SlimeSword.prefab'), projPrefab(SWORD_ROOT, G.sword, 'SlimeSword', 50, 14, 0.9));
w(path.join(PREFABDIR, 'SlimeSword.prefab.meta'), prefabMeta(G.pf_sword));
w(path.join(PREFABDIR, 'SlimeArrow.prefab'), projPrefab(ARROW_ROOT, G.arrow, 'SlimeArrow', 35, 20, 0.7));
w(path.join(PREFABDIR, 'SlimeArrow.prefab.meta'), prefabMeta(G.pf_arrow));

// script metas
w(path.join(CSDIR, 'SlimeBoss.cs.meta'), scriptMeta(G.sc_boss));
w(path.join(CSDIR, 'SlimeBossProjectile.cs.meta'), scriptMeta(G.sc_proj));

console.log('ALL ASSETS GENERATED');
