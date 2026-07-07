const { PNG } = require('pngjs');
const fs = require('fs');
const path = require('path');
const c = require('crypto');
const RAW = path.resolve(__dirname);
const MAP = [
  ['Pixel_art_UI_health_bar_empty__2026-07-07T02-02-36.png', 'BossBarBg.png'],
  ['Pixel_art_UI_health_bar_fill___2026-07-07T02-02-36.png', 'BossBarFill.png'],
];
function isGreen(r, g, b) { return g > 90 && g > r + 25 && g > b + 20; }
for (const [src, dst] of MAP) {
  const p = PNG.sync.read(fs.readFileSync(path.join(RAW, src)));
  const W = p.width, H = p.height, d = p.data;
  for (let i = 0; i < d.length; i += 4) if (isGreen(d[i],d[i+1],d[i+2])) d[i+3] = 0;
  let minX=W,minY=H,maxX=-1,maxY=-1;
  for (let y=0;y<H;y++) for (let x=0;x<W;x++) if (d[(y*W+x)*4+3]>16) {
    if(x<minX)minX=x;if(x>maxX)maxX=x;if(y<minY)minY=y;if(y>maxY)maxY=y;
  }
  const pad=4; minX=Math.max(0,minX-pad);minY=Math.max(0,minY-pad);
  maxX=Math.min(W-1,maxX+pad);maxY=Math.min(H-1,maxY+pad);
  const cw=maxX-minX+1,ch=maxY-minY+1;
  const out=new PNG({width:cw,height:ch});
  for(let y=0;y<ch;y++) for(let x=0;x<cw;x++){
    const si=((minY+y)*W+minX+x)*4,di=(y*cw+x)*4;
    out.data[di]=d[si];out.data[di+1]=d[si+1];out.data[di+2]=d[si+2];out.data[di+3]=d[si+3];
  }
  fs.writeFileSync(path.join(RAW,dst),PNG.sync.write(out));
  console.log(dst.padEnd(20),cw+'x'+ch);
}
// generate metas with proper sprite border for Sliced
for (const name of ['BossBarBg','BossBarFill']) {
  const g = c.randomBytes(16).toString('hex');
  // Bg: Sliced with 6px border; Fill: Simple (will be used as Filled clipped)
  const isBg = name === 'BossBarBg';
  const border = isBg ? 'spriteBorder: {x: 6, y: 6, z: 6, w: 6}' : 'spriteBorder: {x: 0, y: 0, z: 0, w: 0}';
  const ppu = isBg ? 100 : 100;
  fs.writeFileSync(path.join(RAW, name+'.png.meta'),
`fileFormatVersion: 2
guid: ${g}
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
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: ${ppu}
  ${border}
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
    spriteID: ${g}
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
`);
  console.log('META '+name+' = '+g);
}
