#import <AppKit/AppKit.h>
#import <Metal/Metal.h>
#import <QuartzCore/CAMetalLayer.h>

#if __has_include(<MetalFX/MetalFX.h>)
#import <MetalFX/MetalFX.h>
#define FCR_HAS_METALFX 1
#else
#define FCR_HAS_METALFX 0
#endif

typedef NS_ENUM(uint32_t, FCRUpscaleMode) {
    FCRUpscaleModeNone = 0,
    FCRUpscaleModeSpatial = 1,
    FCRUpscaleModeTemporal = 2
};

typedef NS_ENUM(uint32_t, FCRUpscaleOutputResolution) {
    FCRUpscaleOutputResolution1080p = 0,
    FCRUpscaleOutputResolution1440p = 1,
    FCRUpscaleOutputResolution2160p = 2
};

typedef NS_ENUM(uint32_t, FCRUpscaleFallbackReason) {
    FCRUpscaleFallbackReasonNone = 0,
    FCRUpscaleFallbackReasonUnsupportedPlatform = 1,
    FCRUpscaleFallbackReasonUnsupportedDevice = 2,
    FCRUpscaleFallbackReasonOutputSmallerThanInput = 3,
    FCRUpscaleFallbackReasonScalerCreationFailed = 4,
    FCRUpscaleFallbackReasonRuntimeCommandFailure = 5,
    FCRUpscaleFallbackReasonRequestedPathUnavailable = 6
};

typedef NS_ENUM(uint32_t, FCRTemporalResetReason) {
    FCRTemporalResetReasonNone = 0,
    FCRTemporalResetReasonPresenterRecreated = 1,
    FCRTemporalResetReasonRomLoaded = 2,
    FCRTemporalResetReasonSaveStateLoaded = 3,
    FCRTemporalResetReasonUpscaleModeChanged = 4,
    FCRTemporalResetReasonRuntimeFallback = 5,
    FCRTemporalResetReasonTimelineJump = 6
};

typedef struct FCRPresenterDiagnostics {
    uint32_t requestedUpscaleMode;
    uint32_t effectiveUpscaleMode;
    uint32_t fallbackReason;
    uint32_t internalWidth;
    uint32_t internalHeight;
    uint32_t outputWidth;
    uint32_t outputHeight;
    uint32_t drawableWidth;
    uint32_t drawableHeight;
    double targetWidthPoints;
    double targetHeightPoints;
    double displayScale;
    double hostWidthPoints;
    double hostHeightPoints;
    double layerWidthPoints;
    double layerHeightPoints;
    uint32_t temporalResetPending;
    uint32_t temporalResetApplied;
    uint32_t temporalResetCount;
    uint32_t temporalResetReason;
} FCRPresenterDiagnostics;

typedef struct __attribute__((packed)) FCRBackgroundTile {
    float screenX;
    float screenY;
    uint32_t tileId;
    uint32_t paletteBaseIndex;
    float clipTop;
    float clipBottom;
} FCRBackgroundTile;

typedef struct __attribute__((packed)) FCRSpriteTile {
    float screenX;
    float screenY;
    uint32_t tileId;
    uint32_t paletteBaseIndex;
    uint32_t flipH;
    uint32_t flipV;
    uint32_t behindBackground;
    uint32_t originalOamIndex;
} FCRSpriteTile;

static const uint32_t kFcrTileSize = 8;
static const uint32_t kFcrAtlasWidth = 128;
static const uint32_t kFcrAtlasHeight = 256;

@interface FCRMetalPresenter : NSObject
@property (nonatomic, strong) NSView* hostView;
@property (nonatomic, strong) NSView* renderView;
@property (nonatomic, strong) CAMetalLayer* metalLayer;
@property (nonatomic, strong) id<MTLDevice> device;
@property (nonatomic, assign) NSUInteger frameWidth;
@property (nonatomic, assign) NSUInteger frameHeight;
@property (nonatomic, assign) CGSize targetSizePoints;
@property (nonatomic, assign) CGFloat displayScale;
@property (nonatomic, assign) FCRUpscaleMode requestedUpscaleMode;
@property (nonatomic, assign) FCRUpscaleMode effectiveUpscaleMode;
@property (nonatomic, assign) FCRUpscaleFallbackReason fallbackReason;
@property (nonatomic, assign) FCRUpscaleOutputResolution outputResolution;
@property (nonatomic, assign) FCRTemporalResetReason temporalResetReason;
@property (nonatomic, assign) uint32_t temporalResetCount;
@property (nonatomic, assign) BOOL temporalResetPending;
@property (nonatomic, assign) BOOL temporalResetApplied;
#if FCR_HAS_METALFX
@property (nonatomic, strong) id<MTLCommandQueue> commandQueue;
@property (nonatomic, strong) id<MTLFXSpatialScaler> spatialScaler;
@property (nonatomic, assign) BOOL spatialSupported;
#endif
- (instancetype)initWithParentView:(NSView*)parentView
                         frameWidth:(NSUInteger)frameWidth
                        frameHeight:(NSUInteger)frameHeight
                        upscaleMode:(FCRUpscaleMode)upscaleMode;
- (BOOL)presentFrameWithPixels:(const uint32_t*)pixels
                    pixelCount:(size_t)pixelCount;
- (void)setUpscaleMode:(FCRUpscaleMode)mode;
- (void)setDisplaySizeWithWidthPoints:(double)widthPoints
                         heightPoints:(double)heightPoints;
- (void)setCornerRadius:(double)radiusPoints;
- (void)requestTemporalHistoryReset:(FCRTemporalResetReason)reason;
- (void)invalidate;
- (void)populateDiagnostics:(FCRPresenterDiagnostics*)diagnostics;
#if FCR_HAS_METALFX
- (BOOL)tryEnableSpatialScaler;
- (FCRUpscaleFallbackReason)temporalFallbackReason;
#endif
- (void)applyPendingTemporalHistoryResetIfNeeded;
- (void)resetUpscaleState;
- (void)applyRuntimeFallbackToNone;
@end

@implementation FCRMetalPresenter

- (instancetype)initWithParentView:(NSView*)parentView
                         frameWidth:(NSUInteger)frameWidth
                        frameHeight:(NSUInteger)frameHeight
                        upscaleMode:(FCRUpscaleMode)upscaleMode
{
    self = [super init];
    if (self == nil || parentView == nil || frameWidth == 0 || frameHeight == 0) {
        return nil;
    }

    _device = MTLCreateSystemDefaultDevice();
    if (_device == nil) {
        return nil;
    }

    _frameWidth = frameWidth;
    _frameHeight = frameHeight;
    _displayScale = parentView.window != nil ? parentView.window.backingScaleFactor : NSScreen.mainScreen.backingScaleFactor;
    if (_displayScale <= 0.0) {
        _displayScale = 1.0;
    }
    _requestedUpscaleMode = upscaleMode;
    _effectiveUpscaleMode = FCRUpscaleModeNone;
    _fallbackReason = FCRUpscaleFallbackReasonNone;
    _outputResolution = FCRUpscaleOutputResolution2160p;

    _hostView = [[NSView alloc] initWithFrame:parentView.bounds];
    _hostView.wantsLayer = YES;
    _hostView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;

    _renderView = [[NSView alloc] initWithFrame:_hostView.bounds];
    _renderView.wantsLayer = YES;
    _renderView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;

    _metalLayer = [CAMetalLayer layer];
    _metalLayer.device = _device;
    _metalLayer.pixelFormat = MTLPixelFormatBGRA8Unorm;
    _metalLayer.framebufferOnly = NO;
    _metalLayer.contentsGravity = kCAGravityResizeAspect;
    _metalLayer.contentsScale = _displayScale;
    _metalLayer.frame = _renderView.bounds;
    _metalLayer.needsDisplayOnBoundsChange = YES;
    _renderView.layer = _metalLayer;

    [_hostView addSubview:_renderView];
    [parentView addSubview:_hostView];

    _targetSizePoints = parentView.bounds.size;
    if (_targetSizePoints.width <= 0.0 || _targetSizePoints.height <= 0.0) {
        _targetSizePoints = CGSizeMake((CGFloat)frameWidth, (CGFloat)frameHeight);
    }

#if FCR_HAS_METALFX
    _commandQueue = [_device newCommandQueue];
    _spatialSupported = [_device supportsFamily:MTLGPUFamilyApple7] ||
                        [_device supportsFamily:MTLGPUFamilyMac2];
#endif
    [self setUpscaleMode:upscaleMode];
    [self refreshLayerFrameAndDrawableSize];

    return self;
}

- (void)setUpscaleMode:(FCRUpscaleMode)mode
{
    _requestedUpscaleMode = mode;
    [self resetUpscaleState];

    if (mode == FCRUpscaleModeNone) {
        return;
    }

#if FCR_HAS_METALFX
    if (mode == FCRUpscaleModeTemporal) {
        FCRUpscaleFallbackReason temporalReason = [self temporalFallbackReason];
        if ([self tryEnableSpatialScaler]) {
            _fallbackReason = temporalReason;
            return;
        }

        if (_fallbackReason == FCRUpscaleFallbackReasonNone) {
            _fallbackReason = temporalReason;
        }
        return;
    }

    if (mode == FCRUpscaleModeSpatial) {
        [self tryEnableSpatialScaler];
        return;
    }
#else
    if (mode == FCRUpscaleModeSpatial || mode == FCRUpscaleModeTemporal) {
        _fallbackReason = FCRUpscaleFallbackReasonUnsupportedPlatform;
        return;
    }
#endif
}

- (void)resetUpscaleState
{
#if FCR_HAS_METALFX
    _spatialScaler = nil;
#endif
    _effectiveUpscaleMode = FCRUpscaleModeNone;
    _fallbackReason = FCRUpscaleFallbackReasonNone;
}

- (void)applyRuntimeFallbackToNone
{
#if FCR_HAS_METALFX
    _spatialScaler = nil;
#endif
    _effectiveUpscaleMode = FCRUpscaleModeNone;
    _fallbackReason = FCRUpscaleFallbackReasonRuntimeCommandFailure;
}

#if FCR_HAS_METALFX
- (BOOL)tryEnableSpatialScaler
{
    if (@available(macOS 14.0, *)) {
        if (!_spatialSupported || _commandQueue == nil) {
            _fallbackReason = FCRUpscaleFallbackReasonUnsupportedDevice;
            return NO;
        }

        _spatialScaler = [self createSpatialScaler];
        if (_spatialScaler != nil) {
            _effectiveUpscaleMode = FCRUpscaleModeSpatial;
            _fallbackReason = FCRUpscaleFallbackReasonNone;
            return YES;
        }

        if (_fallbackReason == FCRUpscaleFallbackReasonNone) {
            _fallbackReason = FCRUpscaleFallbackReasonScalerCreationFailed;
        }
        return NO;
    }

    _fallbackReason = FCRUpscaleFallbackReasonUnsupportedPlatform;
    return NO;
}

- (FCRUpscaleFallbackReason)temporalFallbackReason
{
    if (@available(macOS 14.0, *)) {
        if (!_spatialSupported || _commandQueue == nil) {
            return FCRUpscaleFallbackReasonUnsupportedDevice;
        }

        // Temporal on-screen runtime is not wired yet, so a Temporal request currently
        // falls back to Spatial when available.
        return FCRUpscaleFallbackReasonRequestedPathUnavailable;
    }

    return FCRUpscaleFallbackReasonUnsupportedPlatform;
}
#endif

- (void)setDisplaySizeWithWidthPoints:(double)widthPoints
                         heightPoints:(double)heightPoints
{
    if (widthPoints <= 0.0 || heightPoints <= 0.0) {
        return;
    }

    _targetSizePoints = CGSizeMake((CGFloat)widthPoints, (CGFloat)heightPoints);
    [self refreshLayerFrameAndDrawableSize];
}

- (void)setCornerRadius:(double)radiusPoints
{
    CGFloat cornerRadius = (CGFloat)MAX(0.0, radiusPoints);
    _renderView.wantsLayer = YES;
    _renderView.layer.cornerRadius = cornerRadius;
    _renderView.layer.masksToBounds = cornerRadius > 0.0;
}

- (void)requestTemporalHistoryReset:(FCRTemporalResetReason)reason
{
    if (reason == FCRTemporalResetReasonNone) {
        return;
    }

    _temporalResetReason = reason;
    _temporalResetPending = YES;
    _temporalResetApplied = NO;
}

- (void)invalidate
{
    [_renderView removeFromSuperview];
    [_hostView removeFromSuperview];
    _renderView = nil;
    _hostView = nil;
    _metalLayer = nil;
#if FCR_HAS_METALFX
    _spatialScaler = nil;
    _commandQueue = nil;
#endif
}

- (BOOL)presentFrameWithPixels:(const uint32_t*)pixels
                    pixelCount:(size_t)pixelCount
{
    if (_metalLayer == nil || _device == nil || pixels == NULL) {
        return NO;
    }

    const size_t expectedPixelCount = _frameWidth * _frameHeight;
    if (pixelCount < expectedPixelCount) {
        return NO;
    }

    [self refreshLayerFrameAndDrawableSize];

    id<CAMetalDrawable> drawable = [_metalLayer nextDrawable];
    if (drawable == nil) {
        return NO;
    }

    id<MTLTexture> drawableTexture = drawable.texture;
    if (drawableTexture == nil) {
        return NO;
    }

    MTLRegion inputRegion = MTLRegionMake2D(0, 0, _frameWidth, _frameHeight);
    MTLTextureDescriptor* inputDescriptor = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm
                                                                                                width:_frameWidth
                                                                                               height:_frameHeight
                                                                                            mipmapped:NO];
    inputDescriptor.usage = MTLTextureUsageShaderRead | MTLTextureUsageShaderWrite;
    id<MTLTexture> inputTexture = [_device newTextureWithDescriptor:inputDescriptor];
    if (inputTexture == nil) {
        return NO;
    }

    [inputTexture replaceRegion:inputRegion
                    mipmapLevel:0
                      withBytes:pixels
                    bytesPerRow:_frameWidth * sizeof(uint32_t)];

    [self applyPendingTemporalHistoryResetIfNeeded];

#if FCR_HAS_METALFX
    if (_effectiveUpscaleMode == FCRUpscaleModeSpatial && _spatialScaler != nil && _commandQueue != nil) {
        id<MTLCommandBuffer> commandBuffer = [_commandQueue commandBuffer];
        if (commandBuffer == nil) {
            [self applyRuntimeFallbackToNone];
            return [self presentFrameWithPixels:pixels pixelCount:pixelCount];
        }

        _spatialScaler.colorTexture = inputTexture;
        _spatialScaler.outputTexture = drawableTexture;
        [_spatialScaler encodeToCommandBuffer:commandBuffer];
        [commandBuffer presentDrawable:drawable];
        [commandBuffer commit];
        [commandBuffer waitUntilCompleted];

        if (commandBuffer.status == MTLCommandBufferStatusError) {
            [self applyRuntimeFallbackToNone];
            return [self presentFrameWithPixels:pixels pixelCount:pixelCount];
        }

        return YES;
    }
#endif

    [drawableTexture replaceRegion:MTLRegionMake2D(0, 0, MIN(_frameWidth, drawableTexture.width), MIN(_frameHeight, drawableTexture.height))
                       mipmapLevel:0
                         withBytes:pixels
                       bytesPerRow:_frameWidth * sizeof(uint32_t)];
    [drawable present];
    return YES;
}

- (void)populateDiagnostics:(FCRPresenterDiagnostics*)diagnostics
{
    if (diagnostics == NULL) {
        return;
    }

    diagnostics->requestedUpscaleMode = (uint32_t)_requestedUpscaleMode;
    diagnostics->effectiveUpscaleMode = (uint32_t)_effectiveUpscaleMode;
    diagnostics->fallbackReason = (uint32_t)_fallbackReason;
    diagnostics->internalWidth = (uint32_t)_frameWidth;
    diagnostics->internalHeight = (uint32_t)_frameHeight;
    diagnostics->outputWidth = (uint32_t)_metalLayer.drawableSize.width;
    diagnostics->outputHeight = (uint32_t)_metalLayer.drawableSize.height;
    diagnostics->drawableWidth = (uint32_t)_metalLayer.drawableSize.width;
    diagnostics->drawableHeight = (uint32_t)_metalLayer.drawableSize.height;
    diagnostics->targetWidthPoints = _targetSizePoints.width;
    diagnostics->targetHeightPoints = _targetSizePoints.height;
    diagnostics->displayScale = _displayScale;
    diagnostics->hostWidthPoints = _hostView.bounds.size.width;
    diagnostics->hostHeightPoints = _hostView.bounds.size.height;
    diagnostics->layerWidthPoints = _renderView.bounds.size.width;
    diagnostics->layerHeightPoints = _renderView.bounds.size.height;
    diagnostics->temporalResetPending = _temporalResetPending ? 1u : 0u;
    diagnostics->temporalResetApplied = _temporalResetApplied ? 1u : 0u;
    diagnostics->temporalResetCount = _temporalResetCount;
    diagnostics->temporalResetReason = (uint32_t)_temporalResetReason;
}

#if FCR_HAS_METALFX
- (id<MTLFXSpatialScaler>)createSpatialScaler API_AVAILABLE(macos(14.0))
{
    if (!_spatialSupported || _device == nil) {
        return nil;
    }

    CGSize outputSize = [self requestedOutputPixelSize];
    if (outputSize.width < _frameWidth || outputSize.height < _frameHeight) {
        _fallbackReason = FCRUpscaleFallbackReasonOutputSmallerThanInput;
        return nil;
    }

    MTLFXSpatialScalerDescriptor* descriptor = [[MTLFXSpatialScalerDescriptor alloc] init];
    descriptor.inputWidth = (NSUInteger)_frameWidth;
    descriptor.inputHeight = (NSUInteger)_frameHeight;
    descriptor.outputWidth = (NSUInteger)outputSize.width;
    descriptor.outputHeight = (NSUInteger)outputSize.height;
    descriptor.colorTextureFormat = MTLPixelFormatBGRA8Unorm;
    descriptor.outputTextureFormat = MTLPixelFormatBGRA8Unorm;
    return [descriptor newSpatialScalerWithDevice:_device];
}
#endif

- (CGSize)requestedOutputPixelSize
{
    switch (_outputResolution) {
        case FCRUpscaleOutputResolution1080p:
            return CGSizeMake(1920, 1080);
        case FCRUpscaleOutputResolution1440p:
            return CGSizeMake(2560, 1440);
        case FCRUpscaleOutputResolution2160p:
        default:
            return CGSizeMake(3840, 2160);
    }
}

- (void)refreshLayerFrameAndDrawableSize
{
    if (_hostView == nil || _renderView == nil || _metalLayer == nil) {
        return;
    }

    NSRect hostBounds = _hostView.bounds;
    CGSize targetPoints = _targetSizePoints;
    if (targetPoints.width <= 0.0 || targetPoints.height <= 0.0) {
        targetPoints = hostBounds.size;
    }

    const CGFloat scale = _displayScale > 0.0 ? _displayScale : 1.0;
    CGFloat targetPixelWidth = targetPoints.width * scale;
    CGFloat targetPixelHeight = targetPoints.height * scale;

    const CGFloat sourceAspect = (CGFloat)_frameWidth / (CGFloat)_frameHeight;
    CGFloat drawPixelWidth = targetPixelWidth;
    CGFloat drawPixelHeight = targetPixelHeight;

    if (drawPixelWidth / drawPixelHeight > sourceAspect) {
        drawPixelWidth = drawPixelHeight * sourceAspect;
    } else {
        drawPixelHeight = drawPixelWidth / sourceAspect;
    }

    if (drawPixelWidth < 1.0) {
        drawPixelWidth = 1.0;
    }
    if (drawPixelHeight < 1.0) {
        drawPixelHeight = 1.0;
    }

    const CGFloat drawPointWidth = drawPixelWidth / scale;
    const CGFloat drawPointHeight = drawPixelHeight / scale;
    NSRect drawFrame = NSMakeRect((hostBounds.size.width - drawPointWidth) * 0.5,
                                  (hostBounds.size.height - drawPointHeight) * 0.5,
                                  drawPointWidth,
                                  drawPointHeight);

    _renderView.frame = drawFrame;
    _metalLayer.frame = _renderView.bounds;
    _metalLayer.contentsScale = scale;

#if FCR_HAS_METALFX
    if (_effectiveUpscaleMode == FCRUpscaleModeSpatial && _spatialScaler != nil) {
        _metalLayer.drawableSize = [self requestedOutputPixelSize];
    } else {
        _metalLayer.drawableSize = CGSizeMake(drawPixelWidth, drawPixelHeight);
    }
#else
    _metalLayer.drawableSize = CGSizeMake(drawPixelWidth, drawPixelHeight);
#endif
}

- (void)applyPendingTemporalHistoryResetIfNeeded
{
    if (!_temporalResetPending) {
        return;
    }

    _temporalResetPending = NO;
    _temporalResetApplied = YES;
    _temporalResetCount += 1;
}

@end

static inline uint8_t fcr_read_atlas_color(const uint8_t* atlas, size_t atlasLength, uint32_t tileId, uint32_t sampleX, uint32_t sampleY)
{
    const uint32_t atlasTileRow = tileId / (kFcrAtlasWidth / kFcrTileSize);
    const uint32_t atlasTileColumn = tileId % (kFcrAtlasWidth / kFcrTileSize);
    const uint32_t atlasX = atlasTileColumn * kFcrTileSize + sampleX;
    const uint32_t atlasY = atlasTileRow * kFcrTileSize + sampleY;
    const size_t atlasIndex = (size_t)atlasY * kFcrAtlasWidth + atlasX;
    if (atlas == NULL || atlasIndex >= atlasLength) {
        return 0;
    }
    return atlas[atlasIndex];
}

static inline uint32_t fcr_read_palette_color(const uint32_t* palette, size_t paletteLength, uint32_t paletteBaseIndex, uint8_t colorIndex)
{
    if (palette == NULL || paletteLength == 0) {
        return 0xFF000000u;
    }

    uint32_t paletteIndex = (paletteBaseIndex + colorIndex) & 0x1Fu;
    if ((paletteIndex & 0x13u) == 0x10u) {
        paletteIndex &= 0x0Fu;
    }

    return palette[paletteIndex % paletteLength];
}

static uint32_t fcr_hash_bytes_fnv1a32(const uint8_t* bytes, size_t length)
{
    uint32_t hash = 2166136261u;
    if (bytes == NULL || length == 0) {
        return hash;
    }

    for (size_t i = 0; i < length; i++) {
        hash ^= bytes[i];
        hash *= 16777619u;
    }

    return hash;
}

static inline uint32_t fcr_hash_uint32_fnv1a32(uint32_t hash, uint32_t value)
{
    for (int shift = 0; shift < 32; shift += 8) {
        hash ^= (uint8_t)((value >> shift) & 0xFFu);
        hash *= 16777619u;
    }

    return hash;
}

static inline uint32_t fcr_temporal_verification_marker(
    const uint8_t* motionTextureBytes,
    uint32_t motionTextureByteLength,
    uint32_t motionTextureWidth,
    uint32_t motionTextureHeight)
{
    uint32_t hash = fcr_hash_bytes_fnv1a32(motionTextureBytes, motionTextureByteLength);
    hash = fcr_hash_uint32_fnv1a32(hash, motionTextureWidth);
    hash = fcr_hash_uint32_fnv1a32(hash, motionTextureHeight);
    hash = (hash & 0x00FFFFFFu) | 0xFF000000u;
    return (hash & 0x00FFFFFFu) == 0 ? 0xFF010203u : hash;
}

static inline void fcr_apply_temporal_verification_marker(
    FCRUpscaleMode upscaleMode,
    const uint8_t* motionTextureBytes,
    uint32_t motionTextureByteLength,
    uint32_t motionTextureWidth,
    uint32_t motionTextureHeight,
    uint32_t* outputPixels,
    size_t outputPixelCount)
{
    // This is an offscreen verification hook only. It proves the payload reached native code
    // without claiming that Temporal MetalFX runtime integration exists here.
    if (upscaleMode != FCRUpscaleModeTemporal ||
        motionTextureBytes == NULL ||
        motionTextureByteLength == 0 ||
        outputPixels == NULL ||
        outputPixelCount == 0) {
        return;
    }

    outputPixels[outputPixelCount - 1] = fcr_temporal_verification_marker(
        motionTextureBytes,
        motionTextureByteLength,
        motionTextureWidth,
        motionTextureHeight);
}

bool FCR_RenderLayeredFrameOffscreen(
    const uint8_t* chrAtlas,
    uint32_t chrAtlasLength,
    const uint32_t* palette,
    uint32_t paletteLength,
    const FCRBackgroundTile* backgroundTiles,
    uint32_t backgroundTileCount,
    const FCRSpriteTile* sprites,
    uint32_t spriteCount,
    uint8_t showBackground,
    uint8_t showSprites,
    uint8_t showBackgroundLeft8,
    uint8_t showSpritesLeft8,
    uint32_t frameWidth,
    uint32_t frameHeight,
    uint32_t* outputPixels,
    uint32_t outputPixelCount)
{
    if (frameWidth == 0 || frameHeight == 0 || outputPixels == NULL) {
        return false;
    }

    const size_t pixelCount = (size_t)frameWidth * (size_t)frameHeight;
    if (outputPixelCount < pixelCount) {
        return false;
    }

    uint32_t backdropColor = paletteLength > 0 ? palette[0] : 0xFF000000u;

    uint32_t* backgroundColors = calloc(pixelCount, sizeof(uint32_t));
    uint32_t* spriteColors = calloc(pixelCount, sizeof(uint32_t));
    uint8_t* backgroundOpaque = calloc(pixelCount, sizeof(uint8_t));
    uint8_t* spriteState = calloc(pixelCount, sizeof(uint8_t));
    if (backgroundColors == NULL || spriteColors == NULL || backgroundOpaque == NULL || spriteState == NULL) {
        free(backgroundColors);
        free(spriteColors);
        free(backgroundOpaque);
        free(spriteState);
        return false;
    }

    for (size_t i = 0; i < pixelCount; i++) {
        backgroundColors[i] = backdropColor;
    }

    if (showBackground) {
        for (uint32_t tileIndex = 0; tileIndex < backgroundTileCount; tileIndex++) {
            const FCRBackgroundTile tile = backgroundTiles[tileIndex];
            const int32_t originX = (int32_t)tile.screenX;
            const int32_t originY = (int32_t)tile.screenY;
            const int32_t clipTop = (int32_t)tile.clipTop;
            const int32_t clipBottom = tile.clipBottom <= tile.clipTop ? (int32_t)frameHeight : (int32_t)tile.clipBottom;

            for (uint32_t localY = 0; localY < kFcrTileSize; localY++) {
                int32_t pixelY = originY + (int32_t)localY;
                if (pixelY < 0 || pixelY >= (int32_t)frameHeight) {
                    continue;
                }

                for (uint32_t localX = 0; localX < kFcrTileSize; localX++) {
                    int32_t pixelX = originX + (int32_t)localX;
                    if (pixelX < 0 || pixelX >= (int32_t)frameWidth) {
                        continue;
                    }
                    if (!showBackgroundLeft8 && pixelX < (int32_t)kFcrTileSize) {
                        continue;
                    }
                    if (pixelY < clipTop || pixelY >= clipBottom) {
                        continue;
                    }

                    uint8_t colorIndex = fcr_read_atlas_color(chrAtlas, chrAtlasLength, tile.tileId, localX, localY);
                    if (colorIndex == 0) {
                        continue;
                    }

                    size_t pixelOffset = (size_t)pixelY * frameWidth + (size_t)pixelX;
                    backgroundColors[pixelOffset] = fcr_read_palette_color(palette, paletteLength, tile.paletteBaseIndex, colorIndex);
                    backgroundOpaque[pixelOffset] = 1;
                }
            }
        }
    }

    if (showSprites) {
        for (int32_t spriteIndex = (int32_t)spriteCount - 1; spriteIndex >= 0; spriteIndex--) {
            const FCRSpriteTile sprite = sprites[spriteIndex];
            const int32_t originX = (int32_t)sprite.screenX;
            const int32_t originY = (int32_t)sprite.screenY;

            for (uint32_t localY = 0; localY < kFcrTileSize; localY++) {
                uint32_t sampleY = sprite.flipV ? (kFcrTileSize - 1 - localY) : localY;
                int32_t pixelY = originY + (int32_t)localY;
                if (pixelY < 0 || pixelY >= (int32_t)frameHeight) {
                    continue;
                }

                for (uint32_t localX = 0; localX < kFcrTileSize; localX++) {
                    int32_t pixelX = originX + (int32_t)localX;
                    if (pixelX < 0 || pixelX >= (int32_t)frameWidth) {
                        continue;
                    }
                    if (!showSpritesLeft8 && pixelX < (int32_t)kFcrTileSize) {
                        continue;
                    }

                    uint32_t sampleX = sprite.flipH ? (kFcrTileSize - 1 - localX) : localX;
                    uint8_t colorIndex = fcr_read_atlas_color(chrAtlas, chrAtlasLength, sprite.tileId, sampleX, sampleY);
                    if (colorIndex == 0) {
                        continue;
                    }

                    size_t pixelOffset = (size_t)pixelY * frameWidth + (size_t)pixelX;
                    spriteColors[pixelOffset] = fcr_read_palette_color(palette, paletteLength, sprite.paletteBaseIndex, colorIndex);
                    spriteState[pixelOffset] = sprite.behindBackground ? 2 : 1;
                }
            }
        }
    }

    for (size_t i = 0; i < pixelCount; i++) {
        const bool hasSprite = spriteState[i] != 0;
        const bool spriteBehindBackground = spriteState[i] == 2;
        outputPixels[i] = hasSprite && !(backgroundOpaque[i] && spriteBehindBackground)
            ? spriteColors[i]
            : backgroundColors[i];
    }

    free(backgroundColors);
    free(spriteColors);
    free(backgroundOpaque);
    free(spriteState);
    return true;
}

bool FCR_RenderLayeredFrameOffscreenEx(
    const uint8_t* chrAtlas,
    uint32_t chrAtlasLength,
    const uint32_t* palette,
    uint32_t paletteLength,
    const FCRBackgroundTile* backgroundTiles,
    uint32_t backgroundTileCount,
    const FCRSpriteTile* sprites,
    uint32_t spriteCount,
    uint8_t showBackground,
    uint8_t showSprites,
    uint8_t showBackgroundLeft8,
    uint8_t showSpritesLeft8,
    uint32_t frameWidth,
    uint32_t frameHeight,
    FCRUpscaleMode upscaleMode,
    uint32_t outputWidth,
    uint32_t outputHeight,
    uint32_t* outputPixels,
    uint32_t outputPixelCount)
{
    (void)upscaleMode;

    if (outputWidth == 0 || outputHeight == 0 || outputPixels == NULL) {
        return false;
    }

    if (outputWidth == frameWidth && outputHeight == frameHeight) {
        return FCR_RenderLayeredFrameOffscreen(
            chrAtlas,
            chrAtlasLength,
            palette,
            paletteLength,
            backgroundTiles,
            backgroundTileCount,
            sprites,
            spriteCount,
            showBackground,
            showSprites,
            showBackgroundLeft8,
            showSpritesLeft8,
            frameWidth,
            frameHeight,
            outputPixels,
            outputPixelCount);
    }

    const size_t inputPixelCount = (size_t)frameWidth * (size_t)frameHeight;
    uint32_t* inputPixels = calloc(inputPixelCount, sizeof(uint32_t));
    if (inputPixels == NULL) {
        return false;
    }

    bool rendered = FCR_RenderLayeredFrameOffscreen(
        chrAtlas,
        chrAtlasLength,
        palette,
        paletteLength,
        backgroundTiles,
        backgroundTileCount,
        sprites,
        spriteCount,
        showBackground,
        showSprites,
        showBackgroundLeft8,
        showSpritesLeft8,
        frameWidth,
        frameHeight,
        inputPixels,
        (uint32_t)inputPixelCount);
    if (!rendered) {
        free(inputPixels);
        return false;
    }

    const size_t outPixelCount = (size_t)outputWidth * (size_t)outputHeight;
    if (outputPixelCount < outPixelCount) {
        free(inputPixels);
        return false;
    }

    for (uint32_t y = 0; y < outputHeight; y++) {
        uint32_t srcY = (uint32_t)((uint64_t)y * frameHeight / outputHeight);
        if (srcY >= frameHeight) {
            srcY = frameHeight - 1;
        }
        for (uint32_t x = 0; x < outputWidth; x++) {
            uint32_t srcX = (uint32_t)((uint64_t)x * frameWidth / outputWidth);
            if (srcX >= frameWidth) {
                srcX = frameWidth - 1;
            }
            outputPixels[(size_t)y * outputWidth + x] = inputPixels[(size_t)srcY * frameWidth + srcX];
        }
    }

    free(inputPixels);
    return true;
}

bool FCR_RenderLayeredFrameOffscreenExWithMotionTexture(
    const uint8_t* chrAtlas,
    uint32_t chrAtlasLength,
    const uint32_t* palette,
    uint32_t paletteLength,
    const FCRBackgroundTile* backgroundTiles,
    uint32_t backgroundTileCount,
    const FCRSpriteTile* sprites,
    uint32_t spriteCount,
    uint8_t showBackground,
    uint8_t showSprites,
    uint8_t showBackgroundLeft8,
    uint8_t showSpritesLeft8,
    uint32_t frameWidth,
    uint32_t frameHeight,
    FCRUpscaleMode upscaleMode,
    uint32_t outputWidth,
    uint32_t outputHeight,
    const uint8_t* motionTextureBytes,
    uint32_t motionTextureByteLength,
    uint32_t motionTextureWidth,
    uint32_t motionTextureHeight,
    uint32_t* outputPixels,
    uint32_t outputPixelCount)
{
    bool rendered = FCR_RenderLayeredFrameOffscreenEx(
        chrAtlas,
        chrAtlasLength,
        palette,
        paletteLength,
        backgroundTiles,
        backgroundTileCount,
        sprites,
        spriteCount,
        showBackground,
        showSprites,
        showBackgroundLeft8,
        showSpritesLeft8,
        frameWidth,
        frameHeight,
        upscaleMode,
        outputWidth,
        outputHeight,
        outputPixels,
        outputPixelCount);
    if (!rendered) {
        return false;
    }

    const size_t renderedPixelCount = (size_t)outputWidth * (size_t)outputHeight;
    fcr_apply_temporal_verification_marker(
        upscaleMode,
        motionTextureBytes,
        motionTextureByteLength,
        motionTextureWidth,
        motionTextureHeight,
        outputPixels,
        renderedPixelCount);
    return true;
}

void* FCR_CreateMetalPresenter(void* parentView, uint32_t frameWidth, uint32_t frameHeight, FCRUpscaleMode upscaleMode)
{
    @autoreleasepool {
        NSView* parent = (__bridge NSView*)parentView;
        FCRMetalPresenter* presenter = [[FCRMetalPresenter alloc] initWithParentView:parent
                                                                           frameWidth:frameWidth
                                                                          frameHeight:frameHeight
                                                                          upscaleMode:upscaleMode];
        return (__bridge_retained void*)presenter;
    }
}

void* FCR_GetMetalPresenterViewHandle(void* presenterHandle)
{
    FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
    if (presenter == nil || presenter.hostView == nil) {
        return NULL;
    }
    return (__bridge void*)presenter.hostView;
}

void FCR_SetMetalPresenterUpscaleMode(void* presenterHandle, FCRUpscaleMode upscaleMode)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        [presenter setUpscaleMode:upscaleMode];
    }
}

void FCR_SetMetalPresenterUpscaleOutputResolution(void* presenterHandle, FCRUpscaleOutputResolution outputResolution)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        if (presenter == nil) {
            return;
        }
        presenter.outputResolution = outputResolution;
        [presenter setUpscaleMode:presenter.requestedUpscaleMode];
        [presenter refreshLayerFrameAndDrawableSize];
    }
}

void FCR_SetMetalPresenterDisplaySize(void* presenterHandle, double widthPoints, double heightPoints)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        [presenter setDisplaySizeWithWidthPoints:widthPoints heightPoints:heightPoints];
    }
}

void FCR_SetMetalPresenterCornerRadius(void* presenterHandle, double radiusPoints)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        [presenter setCornerRadius:radiusPoints];
    }
}

void FCR_RequestMetalPresenterTemporalHistoryReset(void* presenterHandle, FCRTemporalResetReason reason)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        if (presenter == nil) {
            return;
        }

        [presenter requestTemporalHistoryReset:reason];
    }
}

bool FCR_GetMetalPresenterDiagnostics(void* presenterHandle, FCRPresenterDiagnostics* diagnostics)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        if (presenter == nil || diagnostics == NULL) {
            return false;
        }

        [presenter populateDiagnostics:diagnostics];
        return true;
    }
}

bool FCR_PresentFrame(void* presenterHandle, const uint32_t* pixels, uint32_t pixelCount)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        return [presenter presentFrameWithPixels:pixels pixelCount:pixelCount];
    }
}

bool FCR_PresentLayeredFrame(
    void* presenterHandle,
    const uint8_t* chrAtlas,
    uint32_t chrAtlasLength,
    const uint32_t* palette,
    uint32_t paletteLength,
    const FCRBackgroundTile* backgroundTiles,
    uint32_t backgroundTileCount,
    const FCRSpriteTile* sprites,
    uint32_t spriteCount,
    uint8_t showBackground,
    uint8_t showSprites,
    uint8_t showBackgroundLeft8,
    uint8_t showSpritesLeft8)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge FCRMetalPresenter*)presenterHandle;
        if (presenter == nil) {
            return false;
        }

        const size_t pixelCount = presenter.frameWidth * presenter.frameHeight;
        uint32_t* pixels = calloc(pixelCount, sizeof(uint32_t));
        if (pixels == NULL) {
            return false;
        }

        bool rendered = FCR_RenderLayeredFrameOffscreen(
            chrAtlas,
            chrAtlasLength,
            palette,
            paletteLength,
            backgroundTiles,
            backgroundTileCount,
            sprites,
            spriteCount,
            showBackground,
            showSprites,
            showBackgroundLeft8,
            showSpritesLeft8,
            (uint32_t)presenter.frameWidth,
            (uint32_t)presenter.frameHeight,
            pixels,
            (uint32_t)pixelCount);
        if (!rendered) {
            free(pixels);
            return false;
        }

        bool presented = [presenter presentFrameWithPixels:pixels pixelCount:(uint32_t)pixelCount];
        free(pixels);
        return presented;
    }
}

void FCR_DestroyMetalPresenter(void* presenterHandle)
{
    @autoreleasepool {
        FCRMetalPresenter* presenter = (__bridge_transfer FCRMetalPresenter*)presenterHandle;
        [presenter invalidate];
    }
}
