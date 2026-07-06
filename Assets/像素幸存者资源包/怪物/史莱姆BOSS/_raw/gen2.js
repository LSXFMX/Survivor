const fs = require('fs');
const path = require('path');
const ASSETS = path.resolve(__dirname, '..', '..', '..', '..');
const SPRDIR = path.resolve(__dirname, '..');
const PREFABDIR = path.join(ASSETS, '怪物prefab', 'SlimeBoss');

const G = {
  bow:'1c37e1c37ea028a35148eaf791733d0b',
  qi:'d7faab671fa31b7e35d23c8f1fbce078',
  blob:'39f5f4541c424898371bf52f3b823cc1',
  pf_qi:'43f093b15959e9ad90ccd5a23e214fe6',
  pf_blob:'f924943ebc3175528033f3881ea76921',
  sc_proj:'af24e457aa4e9f2aaa18a9813786d537',
};
const MAT = '{fileID: -876546973899608171, guid: 8bef114c142cd4743948e426f1da55a1, type: 3}';

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
function prefabMeta(guid) {
  return `fileFormatVersion: 2\nguid: ${guid}\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
}
function projPrefab(rootId, spriteGuid, name, damage, speed, hitRadius, pass, scale) {
  const GO = BigInt(rootId), TR = GO + 10n, SR = GO + 20n, MB = GO + 30n;
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
  m_LocalScale: {x: ${scale}, y: ${scale}, z: ${scale}}
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
  m_SortingOrder: 3
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
  lifetime: 2
  pass: ${pass}
  hitRadius: ${hitRadius}
`;
}

const w = (p, s) => { fs.writeFileSync(p, s); console.log('  ' + path.relative(ASSETS, p)); };

w(path.join(SPRDIR, 'SlimeBow.png.meta'), spriteMeta(G.bow, 0.5));
w(path.join(SPRDIR, 'SlimeSwordQi.png.meta'), spriteMeta(G.qi, 0.5));
w(path.join(SPRDIR, 'SlimeBlob.png.meta'), spriteMeta(G.blob, 0.5));

// sword-qi: big, piercing (pass 99), moderate speed
w(path.join(PREFABDIR, 'SlimeSwordQi.prefab'), projPrefab('7300000000000000003', G.qi, 'SlimeSwordQi', 60, 12, 1.6, 99, 1.6));
w(path.join(PREFABDIR, 'SlimeSwordQi.prefab.meta'), prefabMeta(G.pf_qi));
// slime blob: big, single hit
w(path.join(PREFABDIR, 'SlimeBlob.prefab'), projPrefab('7300000000000000004', G.blob, 'SlimeBlob', 45, 9, 1.3, 0, 1.3));
w(path.join(PREFABDIR, 'SlimeBlob.prefab.meta'), prefabMeta(G.pf_blob));

console.log('DONE gen2');
