﻿using Ambermoon.Data;
using Ambermoon.Data.Legacy.Serialization;
using Ambermoon.Render;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ambermoon
{
    internal class Intro
    {
        private enum IntroActionType
        {
            Starfield,
            ThalionLogoFlyIn,
            AmbermoonFlyIn,
            TextCommands,
            DisplayObjects,
            TwinlakeAnimation,
            ColorEffect
        }

        private abstract class IntroAction
        {
            protected IntroAction(IntroActionType type)
            {
                Type = type;
            }

            public IntroActionType Type { get; }
            public abstract void Update(long ticks, int frameCounter);
            public abstract void Destroy();

            public static IntroAction CreateAction(IntroActionType actionType, Intro intro, Action finishHandler, IRenderView renderView, long startTicks, IIntroData introData, Func<short> rng, Font introFont, Font introFontLarge)
            {
                return actionType switch
                {
                    IntroActionType.Starfield => new IntroActionStarfield(renderView, rng),
                    IntroActionType.ThalionLogoFlyIn => new IntroActionLogoFlyin(IntroGraphic.ThalionLogo, actionType, renderView, startTicks, finishHandler, introData, introFontLarge),
                    IntroActionType.AmbermoonFlyIn => new IntroActionLogoFlyin(IntroGraphic.Ambermoon, actionType, renderView, startTicks, finishHandler, introData, introFontLarge),
                    IntroActionType.DisplayObjects => new IntroActionDisplayObjects(renderView, startTicks, introFontLarge, introData, finishHandler),
                    IntroActionType.TwinlakeAnimation => new IntroActionTwinlake(renderView, startTicks, introData, finishHandler),
                    IntroActionType.TextCommands => new IntroActionTextCommands(renderView, introData, introFont, intro, finishHandler, startTicks),
                    _ => throw new NotImplementedException()
                };
            }
        }

        private class IntroActionStarfield : IntroAction
        {
            private readonly IColoredRect[] stars = new IColoredRect[150];
            private readonly Position[] starBasePositions = new Position[150];
            private readonly int[] starScaleValues = new int[150];
            private readonly static List<int> scaleValues = new();
            private readonly int star1InitialScaleValue;
            private const int StartScale = 0x7630;
            private const int ScaleDecrease = 250;
            private long stopTicks = -1;

            static IntroActionStarfield()
            {
                int scale = StartScale;

                do
                {
                    scaleValues.Add(scale);
                    scale -= ScaleDecrease;
                } while (scale >= 0);
            }

            public IntroActionStarfield(IRenderView renderView, Func<short> rng)
                : base(IntroActionType.Starfield)
            {
                var layer = renderView.GetLayer(Layer.IntroEffects);

                for (int i = 0; i < stars.Length; i++)
                {
                    var star = stars[i] = renderView.ColoredRectFactory.Create(1, 1, Color.White, 0);
                    star.Layer = layer;
                    star.Visible = true;

                    var r1 = (rng() % 0x3e80) - 0x1f40;
                    var r2 = (rng() % 0x6400) - 0x3200;

                    r1 *= 256;
                    r2 *= 256;

                    var basePosition = starBasePositions[i] = new Position(r2, r1); // note: they are swapped in original as well!

                    // The original code works a bit different. We need the amount of visible positions per star for some calculation.
                    int currentScale = StartScale;
                    int validPositionCount = 0;

                    while (currentScale > 0)
                    {
                        int x = 160 + basePosition.X / currentScale;
                        int y = 100 + basePosition.Y / currentScale;

                        if (x >= 0 && y >= 0 && x < 320 && y < 200)
                            ++validPositionCount;

                        currentScale -= ScaleDecrease;
                    }

                    // This is the start frame or start scale
                    // of this star.
                    var r = rng() % validPositionCount;
                    starScaleValues[i] = scaleValues[r];

                    if (i == 1)
                        star1InitialScaleValue = starScaleValues[i];
                }
            }

            public void StopAt(long ticks) => stopTicks = ticks;

            public override void Update(long ticks, int frameCounter)
            {
                if (stopTicks != -1 && ticks >= stopTicks && stars[0].X == 0)
                    return;

                for (int i = 0; i < stars.Length; i++)
                {
                    while (true)
                    {
                        var basePosition = starBasePositions[i];
                        var scale = starScaleValues[i];

                        int x = 160 + basePosition.X / scale;
                        int y = 100 + basePosition.Y / scale;

                        scale -= ScaleDecrease;

                        if (scale < 0)
                            scale = StartScale;

                        starScaleValues[i] = scale;

                        if (x >= 0 && y >= 0 && x < 320 && y < 200)
                        {
                            stars[i].X = x;
                            stars[i].Y = 4 + y;
                            break;
                        }
                    }
                }

                // TODO: as we have black borders we might to want to extrapolate the
                // stars to that area. I guess we need 150 more stars which continue at
                // the border. But it's fine for the first version this way.
            }

            public override void Destroy()
            {
                for (int i = 0; i < stars.Length; i++)
                    stars[i]?.Delete();
            }
        }

        private class IntroActionLogoFlyin : IntroAction
        {
            private readonly IAlphaSprite logo;
            private readonly Text presentsText;
            private readonly long startTicks;
            private int scalingFactor = 2048; // start value 0x800
            private readonly int scalingPerTick = 32;
            private readonly int imageWidth;
            private readonly int imageHeight;
            private Action finishHandler;
            private long fadeOutStartTicks = -1;
            private Rect textClipArea;
            private int frameCount;
            // Note: For fade out always palette[2] is used as the target.
            // As it is all white in colors 10 to 1F and there is at least
            // 1 full black color in this area for palette[0] and palette[1]
            // which are used for the logos, we have always the full 15 color
            // change ticks and therefore the fade duration is 15*4 = 60.
            private const int FadeDuration = 60;

            public IntroActionLogoFlyin(IntroGraphic introGraphic, IntroActionType actionType, IRenderView renderView, long startTicks, Action finishHandler, IIntroData introData, Font largeFont)
                : base(actionType)
            {
                // Before starting the scaling, the palette is faded.
                // Nothing is displayed in this time so we can just increase the start ticks.
                this.startTicks = startTicks + 1; // for the +1 see description in Update
                this.finishHandler = finishHandler;

                var size = introGraphic == IntroGraphic.ThalionLogo
                    ? new Size(128, 82) // Thalion logo
                    : new Size(272, 87); // Ambermoon text
                scalingPerTick = actionType == IntroActionType.ThalionLogoFlyIn
                    ? 32 // Thalion logo
                    : 64; // Ambermoon text
                frameCount = 2048 / scalingPerTick;
                logo = renderView.SpriteFactory.CreateWithAlpha(0, 0, 200);
                logo.Layer = renderView.GetLayer(Layer.IntroGraphics);
                logo.TextureSize = size;
                logo.TextureAtlasOffset = TextureAtlasManager.Instance.GetOrCreate(Layer.IntroGraphics).GetOffset((uint)introGraphic);
                logo.PaletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[introGraphic] - 1);
                logo.X = 160; // start in the center
                logo.Y = 100; // start in the center of the 200 height portion
                logo.Alpha = 0;
                logo.Visible = true;

                imageWidth = size.Width;
                imageHeight = size.Height;

                if (actionType == IntroActionType.ThalionLogoFlyIn)
                {
                    textClipArea = new Rect(100, 160, 220, 0);
                    presentsText = largeFont.CreateText(renderView, Layer.IntroText, new Rect(100, 160, 220, 22), introData.Texts[IntroText.Presents], 20, TextAlign.Left, 255, textClipArea);
                    presentsText.Visible = false;
                }
            }

            public override void Update(long ticks, int frameCounter)
            {
                if (ticks < startTicks)
                    return;

                // The original prepares all frames in a buffer.
                // Then
                //   Wait 1 tick
                //   Start palette fade (= fade in)
                //   Wait 1 tick
                // Then for each frame it does:
                //   Wait 1 tick
                //   Render frame (and prepare next)
                //
                // So essentially it starts 1 tick later and then starts the fade.
                // Therefore we increased startTicks by 1 in the constructor.
                // The scaling should only start after 3 ticks (2 ticks as we adjusted startTicks).
                // Thus we added "if (elapsed >= 2)" and "elapsed -= 2" below.

                if (fadeOutStartTicks == -1) // not fading out yet
                {
                    long elapsed = ticks - startTicks;

                    logo.Alpha = (byte)Math.Min(255, elapsed * 255 / FadeDuration);

                    if (elapsed >= 2)
                    {
                        elapsed -= 2;

                        logo.Visible = true;

                        if (scalingFactor >= 256)
                        {
                            scalingFactor = 256 + (int)(2048 - (elapsed * scalingPerTick));

                            if (scalingFactor >= 256)
                            {
                                int width = Math.Max(1, (imageWidth * 256) / scalingFactor);
                                int height = Math.Max(1, (imageHeight * 256) / scalingFactor);
                                logo.Resize(width, height);
                                logo.X = 160 - width / 2;
                                logo.Y = 100 - height / 2;
                            }
                        }

                        // The text is shown after 250 ticks.
                        // The tick counter is reset after the first tick after
                        // starfield setup so it matches our startTicks.
                        elapsed = ticks - startTicks; // use normal value again
                        const int presentShowDelay = 192; // In original it was 250
                        if (presentsText != null && elapsed >= presentShowDelay)
                        {
                            // Thalion logo has the additional text "PRESENTS". It is handled here.
                            // After 250 ticks, the PRESENTS text is shown.
                            presentsText.Visible = true;
                            int oldTextHeight = textClipArea.Height;
                            int newTextHeight = (int)Math.Min(22, 1 + (elapsed - presentShowDelay) / 2); // 2 ticks per pixel line (it reveals directly the first line so use "1 + " here.

                            if (newTextHeight != oldTextHeight)
                            {
                                textClipArea = textClipArea.SetHeight(newTextHeight);
                                presentsText.ClipArea = textClipArea;
                            }

                            // If text was fully displayed,
                            // wait for 220 additional ticks.
                            // Then fade out starts.
                            if (elapsed >= presentShowDelay + 220)
                                fadeOutStartTicks = ticks;
                        }
                        else if (presentsText == null)
                        {
                            // Ambermoon logo has no additional text. It's handled here.
                            // The delay is 260 but this includes the fade out of the previous Thalion logo.
                            // So it would be 260 - 60. But it does not fit exactly so we use a different value.
                            if (elapsed >= 260 - 60 + 30)
                                fadeOutStartTicks = ticks;
                        }
                    }
                }
                else // fading out
                {
                    int fadeAmount = (int)Math.Min(1 + FadeDuration / 4, (ticks - fadeOutStartTicks) / 4);

                    if (fadeAmount == 1 + FadeDuration / 4)
                    {
                        logo.Visible = false;

                        if (presentsText != null)
                            presentsText.Visible = false;

                        finishHandler?.Invoke();
                        finishHandler = null;
                    }
                    else
                    {
                        byte alpha = (byte)Math.Max(0, 255 - fadeAmount * 16);

                        logo.Alpha = alpha;

                        if (presentsText != null)
                            presentsText.Alpha = alpha;
                    }
                }
            }

            public override void Destroy()
            {
                logo?.Delete();
                presentsText?.Destroy();
            }
        }

        private class IntroActionTextCommands : IntroAction
        {
            private readonly IRenderView renderView;
            private readonly IIntroData introData;
            private readonly List<Text> texts = new();
            private readonly Queue<IIntroTextCommand> commands = new();
            private readonly Font introFont;
            private readonly int lineHeight;
            private readonly Intro intro;
            private Data.Enumerations.Color currentTextColor = Data.Enumerations.Color.Black;
            private long waitEndTicks = -1;
            private bool fading = false;
            private int fadeAlphaChange = 0;
            private long lastFadeTicks = 0;
            private readonly long startTicks;
            private long lastTicks;
            private Action finishHandler;

            // To avoid the need for additional palettes just to mimic the dynamic
            // text coloring of the Amiga version, we just map the colors to an
            // appropriate index in the primary UI palette.
            // We know which colors are possible anyway.
            private readonly Dictionary<int, Data.Enumerations.Color> colorMapping = new()
            {
                { 0x0000, Data.Enumerations.Color.Black },
                { 0x0ccc, Data.Enumerations.Color.BrightGray }, // it almost fits with ccb instead of ccc
                { 0x0e92, Data.Enumerations.Color.LightOrange } // it almost fits with f90 instead of e92
            };

            public IntroActionTextCommands(IRenderView renderView, IIntroData introData, Font introFont, Intro intro, Action finishHandler, long startTicks)
                : base(IntroActionType.TextCommands)
            {
                this.renderView = renderView;
                this.introFont = introFont;
                this.introData = introData;
                this.intro = intro;
                this.finishHandler = finishHandler;
                this.startTicks = startTicks;
                lastTicks = startTicks;
                lineHeight = introFont.GlyphGraphics.Values.Select(g => g.Height).Max();

                foreach (var command in introData.TextCommands)
                    commands.Enqueue(command);                
            }

            private static int GetColorFadeDuration(Data.Enumerations.Color color)
            {
                // Dependent on color, the distance in the Amiga differs.
                // Per 4 ticks, 1 component is increased at max until the target color is reached.
                // There are only 4 cases:
                // ccc -> 000
                // e92 -> 000
                // 000 -> ccc
                // 000 -> e92
                // The last two have the same dist and duration than the first two.
                // So the parameter color just gives the non-black color

                return color == Data.Enumerations.Color.BrightGray
                    ? 0xc * 4
                    : 0xe * 4;
            }

            public override void Update(long ticks, int frameCounter)
            {
                if (ticks == lastTicks)
                    return;

                if ((ticks - startTicks) % 4 == 3)
                {
                    lastTicks = ticks;
                    return;
                }

                lastTicks = ticks;

                if (waitEndTicks == -1)
                    waitEndTicks = ticks + 50; // start value

                if (fading)
                {
                    if (fadeAlphaChange != 0)
                    {
                        long fadeIncrements = (ticks - lastFadeTicks) / 4;
                        lastFadeTicks += fadeIncrements * 4;

                        for (int i = 0; i < fadeIncrements && fading; i++)
                        {
                            // update alpha here
                            foreach (var text in texts)
                            {
                                if (fadeAlphaChange > 0 && text.Alpha < 255)
                                    text.Alpha = (byte)Math.Min(255, text.Alpha + fadeAlphaChange);
                                else if (fadeAlphaChange < 0 && text.Alpha > 0)
                                    text.Alpha = (byte)Math.Max(0, text.Alpha + fadeAlphaChange);
                            }

                            int endAlpha = fadeAlphaChange > 0 ? 255 : 0;

                            if (!texts.Any(text => text.Alpha != endAlpha))
                                fading = false;
                        }
                    }
                    else
                    {
                        fading = false;
                    }
                }

                if (waitEndTicks > ticks)
                    return;

                if (commands.Count != 0)
                {
                    var nextCommand = commands.Dequeue();

                    switch (nextCommand.Type)
                    {
                        case IntroTextCommandType.Clear:
                            texts.ForEach(text => text?.Destroy());
                            texts.Clear();
                            break;
                        case IntroTextCommandType.Add:
                            // Texts are displayed in the lower area starting at Y=200
                            AddText(nextCommand.Args[0], 200 + nextCommand.Args[1], nextCommand.Args[2]);
                            break;
                        case IntroTextCommandType.Render:
                            texts.ForEach(text => text.Visible = true);
                            if (currentTextColor != Data.Enumerations.Color.Black)
                                HandleTextColorChange(Data.Enumerations.Color.Black);
                            break;
                        case IntroTextCommandType.Wait:
                            waitEndTicks = ticks + nextCommand.Args[0];
                            break;
                        case IntroTextCommandType.SetTextColor:
                            var oldColor = currentTextColor;
                            if (!colorMapping.TryGetValue(nextCommand.Args[0], out currentTextColor))
                                throw new AmbermoonException(ExceptionScope.Data, "Unsupported intro text color.");
                            HandleTextColorChange(oldColor);
                            break;
                        case IntroTextCommandType.ActivatePaletteFading:
                            intro.StartMeteorGlowing(ticks);
                            break;
                        default:
                            throw new AmbermoonException(ExceptionScope.Data, "Unsupported intro text command.");
                    }

                    void HandleTextColorChange(Data.Enumerations.Color oldColor)
                    {
                        // Note: Black can't be used as a color as index 0 is ignored as a mask color.
                        // Instead use the alpha then. Black is always used for text fade out and
                        // every other color for fade in.
                        if (currentTextColor == Data.Enumerations.Color.Black)
                        {
                            int duration = GetColorFadeDuration(oldColor);
                            fadeAlphaChange = 4 * -255 / duration;
                            fading = true;
                            lastFadeTicks = ticks;
                        }
                        else
                        {
                            int duration = GetColorFadeDuration(currentTextColor);
                            texts.ForEach(text => text.TextColor = currentTextColor);
                            fadeAlphaChange = 4 * 255 / duration;
                            fading = true;
                            lastFadeTicks = ticks;
                        }
                    }
                }
                else
                {
                    finishHandler?.Invoke();
                    finishHandler = null;
                }
            }

            private void AddText(int x, int y, int index)
            {
                if (index == 3 || index == 4) // My name must fit in there :D
                    x -= 20;

                // The clip area is important. Otherwise the virtual screen is used which is only 320x200 and the text only starts at Y = 200.
                var text = introFont.CreateText(renderView, Layer.IntroText, new Rect(x, y, 320 - x, 12), introData.TextCommandTexts[index], 200, TextAlign.Left,
                    0, new Rect(0, 200, 320, 256));
                text.Visible = false;
                text.Place(new Rect(x, y, 320 - x, lineHeight), TextAlign.Left);
                text.TextColor = currentTextColor;
                texts.Add(text);

                if (index == 4) // "Jurie Horneman"
                {
                    // Some self credit for all the hard work. :P
                    y += 14;
                    text = introFont.CreateText(renderView, Layer.IntroText, new Rect(x, y, 320 - x, 12), "ROBERT SCHNECKENHAUS", 200, TextAlign.Left,
                        0, new Rect(0, 200, 320, 256));
                    text.Visible = false;
                    text.Place(new Rect(x, y, 320 - x, lineHeight), TextAlign.Left);
                    text.TextColor = currentTextColor;
                    texts.Add(text);
                }
            }

            public override void Destroy()
            {
                texts.ForEach(text => text?.Destroy());
                texts.Clear();
            }
        }

        private class IntroActionDisplayObjects : IntroAction
        {
            private readonly struct ZoomInfo
            {
                public int EndOffsetX { get; init; }
                public int EndOffsetY { get; init; }
                public int InitialDistance { get; init; }
                public int ImageWidth { get; init; }
                public int ImageHeight { get; init; }
                public int ZoomToWidth { get; init; }
                public int ZoomToHeight { get; init; }
                public IntroGraphic IntroGraphic { get; init; }
            }

            // This is located at the end of the second last data hunk in the ambermoon_intro.
            // There might be a zero word after it. The section starts with the word 00 05
            // which gives the amount of zoom infos.
            // Lyramion, Morag, Forest Moon, Meteor and Sun
            private static readonly ZoomInfo[] ZoomInfos = new ZoomInfo[5]
            {
                new ZoomInfo
                {
                    EndOffsetX = -136,
                    EndOffsetY = -24,
                    InitialDistance = 22000,
                    ImageWidth = 128,
                    ImageHeight = 128,
                    ZoomToWidth = 512,
                    ZoomToHeight = 512,
                    IntroGraphic = IntroGraphic.Lyramion,
                },
                new ZoomInfo
                {
                    EndOffsetX = -256,
                    EndOffsetY = 70,
                    InitialDistance = 21700,
                    ImageWidth = 64,
                    ImageHeight = 64,
                    ZoomToWidth = 256,
                    ZoomToHeight = 256,
                    IntroGraphic = IntroGraphic.Morag,
                },
                new ZoomInfo
                {
                    EndOffsetX = 200,
                    EndOffsetY = -150,
                    InitialDistance = 21500,
                    ImageWidth = 64,
                    ImageHeight = 64,
                    ZoomToWidth = 100,
                    ZoomToHeight = 100,
                    IntroGraphic = IntroGraphic.ForestMoon,
                },
                new ZoomInfo
                {
                    EndOffsetX = -18,
                    EndOffsetY = -210,
                    InitialDistance = 19000,
                    ImageWidth = 96,
                    ImageHeight = 88,
                    ZoomToWidth = 288,
                    ZoomToHeight = 264,
                    IntroGraphic = IntroGraphic.Meteor,
                },
                new ZoomInfo
                {
                    EndOffsetX = -300,
                    EndOffsetY = -32,
                    InitialDistance = 8000,
                    ImageWidth = 64,
                    ImageHeight = 64,
                    ZoomToWidth = 512,
                    ZoomToHeight = 512,
                    IntroGraphic = IntroGraphic.SunAnimation,
                }
            };

            // This is stored between code in the ambermoon_intro.
            // If the current zoom is below MaxZoom, the zoom is
            // increased by Increase each tick. If MaxZoom is exceeded,
            // the next ZoomTransitionInfo is used.
            private record ZoomTransitionInfo(int Increase, int MaxZoom);

            private static readonly ZoomTransitionInfo[] ZoomTransitionInfos = new ZoomTransitionInfo[16]
            {
                new ZoomTransitionInfo(0x10, 0x4268),
                new ZoomTransitionInfo(0x0c, 0x42cc),
                new ZoomTransitionInfo(0x0a, 0x4330),
                new ZoomTransitionInfo(0x08, 0x4394),
                new ZoomTransitionInfo(0x07, 0x43f8),
                new ZoomTransitionInfo(0x06, 0x445c),
                new ZoomTransitionInfo(0x05, 0x44c0),
                new ZoomTransitionInfo(0x04, 0x4524),
                new ZoomTransitionInfo(0x03, 0x4588),
                new ZoomTransitionInfo(0x02, 0x45ec),
                new ZoomTransitionInfo(0x01, 0x4650),
                new ZoomTransitionInfo(0x00, 0x4742), // In this case there is special handling
                new ZoomTransitionInfo(0x01, 0x477c),
                new ZoomTransitionInfo(0x02, 0x47e0),
                new ZoomTransitionInfo(0x03, 0x4a38),
                new ZoomTransitionInfo(0x04, 0x4a9c),
            };

            private readonly struct TownShowInfo
            {
                public int ZoomLevel { get; init; }
                public int Duration { get; init; }
            }

            // This is stored between code in the ambermoon_intro.
            // If the current zoom is above the given zoom, the town
            // image and text is shown for the given duration.
            private static readonly TownShowInfo[] TownShowInfos = new TownShowInfo[3]
            {
                new TownShowInfo { ZoomLevel= 0x52f0, Duration = 200 }, // Gemstone
                new TownShowInfo { ZoomLevel= 0x54c4, Duration = 150 }, // Illien
                new TownShowInfo { ZoomLevel= 0x5654, Duration = 100 }, // Snakesign
            };
            private static readonly int[] TownNameXValues = new int[3]
            {
                92,
                120,
                91
            };

            // Sun is using animationFrameCounter / 4 to get the frame index

            private readonly IRenderView renderView;
            private readonly ITextureAtlas textureAtlas;
            private readonly ILayerSprite[] objects = new ILayerSprite[5];
            private IAlphaSprite glowingMeteorOverlay;
            private readonly IAnimatedLayerSprite[] meteorSparks = new IAnimatedLayerSprite[2];
            private readonly ILayerSprite town;
            private readonly IColoredRect black;
            private readonly Font largeFont;
            private readonly IIntroData introData;
            private Text townText = null;
            // TODO: somewhere the zoom is also set to 14000
            private int currentZoom = -6500; // start value
            private int zoomWaitCounter = -1;
            private const int MaxZoom = 22248;
            private const int FadeOutZoom = 22184;
            private const int MeteorEndZoom = 18868;
            private const int MeteorSparkAppearZoom = 21000;
            private const int MeteorObjectIndex = 3;
            private const int SunObjectIndex = 4;
            private long lastTicks = 0;
            private int zoomTransitionInfoIndex = 0;
            private int meteorSparkFrameCounter = -1;
            private int currentTownIndex = -1;
            private long currentTownStartTicks = -1;
            private int meteorGlowTarget = -1;
            private long lastGlowTicks = 0;
            private readonly int numGlowFadeIncrements = 0; // the amount of changes to fully go from no glow to full glow
            private Action finishHandler;
            private long fadeOutTicks = -1;

            private void CreateTownText(int townIndex)
            {
                townText?.Destroy();
                townText = largeFont.CreateText(renderView, Layer.IntroText, new Rect(TownNameXValues[townIndex], 170, 160, 128),
                    introData.Texts[IntroText.Gemstone + townIndex], 200, TextAlign.Left, 255, new Rect(0, 170, 320, 256));
                townText.Visible = true;
            }

            public IntroActionDisplayObjects(IRenderView renderView, long startTicks, Font largeFont, IIntroData introData, Action finishHandler)
                : base(IntroActionType.DisplayObjects)
            {
                this.renderView = renderView;
                this.largeFont = largeFont;
                this.introData = introData;
                this.finishHandler = finishHandler;
                lastTicks = startTicks;
                var layer = renderView.GetLayer(Layer.IntroGraphics);
                textureAtlas = TextureAtlasManager.Instance.GetOrCreate(Layer.IntroGraphics);
                int textureAtlasWidth = textureAtlas.Texture.Width;

                // Note: The objects are ordered in render order so the
                // last one is drawn over the others. Thus we use the
                // index for the display layer and multiply it by 50.
                for (int i = 0; i < 5; i++)
                {
                    var graphicIndex = i == SunObjectIndex ? IntroGraphic.SunAnimation : IntroGraphic.Lyramion + i;
                    var info = ZoomInfos[i];
                    objects[i] = i == SunObjectIndex
                        ? renderView.SpriteFactory.CreateAnimated(info.ImageWidth, info.ImageHeight, textureAtlasWidth, 12, true, (byte)(4 + i * 50)) as IAnimatedLayerSprite
                        : renderView.SpriteFactory.Create(info.ImageWidth, info.ImageHeight, true, (byte)(4 + i * 50)) as ILayerSprite;
                    objects[i].TextureSize = new Size(info.ImageWidth, info.ImageHeight);
                    objects[i].Layer = layer;
                    objects[i].ClipArea = new Rect(0, 0, 320, 200);
                    objects[i].TextureAtlasOffset = textureAtlas.GetOffset((uint)graphicIndex);
                    objects[i].PaletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[graphicIndex] - 1);
                    objects[i].Visible = false;
                }

                for (int i = 0; i < 2; i++)
                {
                    meteorSparks[i] = renderView.SpriteFactory.CreateAnimated(64, 47, textureAtlasWidth, 15, true, 210) as IAnimatedLayerSprite;
                    meteorSparks[i].Layer = layer;
                    meteorSparks[i].TextureAtlasOffset = textureAtlas.GetOffset((uint)IntroGraphic.MeteorSparks);
                    meteorSparks[i].BaseFrame = (uint)i * 15;
                    meteorSparks[i].PaletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[IntroGraphic.MeteorSparks] - 1);
                    meteorSparks[i].Visible = false;
                    meteorSparks[i].X = 48 + i * 144;
                    meteorSparks[i].Y = 153;
                }

                town = renderView.SpriteFactory.Create(160, 128, true, 150) as ILayerSprite;
                town.Layer = layer;
                town.TextureAtlasOffset = textureAtlas.GetOffset((uint)IntroGraphic.Gemstone);
                town.PaletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[IntroGraphic.Gemstone] - 1);
                town.Visible = false;

                black = renderView.ColoredRectFactory.Create(320, 256, Color.Black, 120);
                black.Layer = renderView.GetLayer(Layer.IntroEffects);
                black.X = 0;
                black.Y = 0;
                black.Visible = false;

                var meteorPalette = introData.IntroPalettes[IntroData.GraphicPalettes[IntroGraphic.Meteor]];
                numGlowFadeIncrements = GetPaletteFadeDuration(meteorPalette, 0, meteorPalette, 16, 16, 1);
            }

            public void StartMeteorGlowing(long ticks)
            {
                meteorGlowTarget = 255;
                var meteor = objects[MeteorObjectIndex];
                glowingMeteorOverlay?.Delete();
                glowingMeteorOverlay = renderView.SpriteFactory.CreateWithAlpha(meteor.Width, meteor.Height, 250);
                glowingMeteorOverlay.TextureSize = new Size(meteor.TextureSize);
                glowingMeteorOverlay.Alpha = 0;
                glowingMeteorOverlay.Layer = meteor.Layer;
                glowingMeteorOverlay.ClipArea = new Rect(0, 0, 320, 200);
                glowingMeteorOverlay.TextureAtlasOffset = textureAtlas.GetOffset((uint)IntroGraphic.GlowingMeteor);
                glowingMeteorOverlay.PaletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[IntroGraphic.GlowingMeteor] - 1);
                glowingMeteorOverlay.X = meteor.X;
                glowingMeteorOverlay.Y = meteor.Y;
                glowingMeteorOverlay.Visible = true;
                lastGlowTicks = ticks;
            }

            public override void Update(long ticks, int frameCounter)
            {
                if (ticks == lastTicks)
                    return;

                long elapsed = ticks - lastTicks;
                lastTicks = ticks;

                CheckTownDisplay(ticks);

                if (!town.Visible)
                {
                    // Update the sun frame
                    (objects[SunObjectIndex] as IAnimatedLayerSprite).CurrentFrame = (uint)frameCounter / 4;

                    if (meteorSparkFrameCounter == -1)
                    {
                        if (currentZoom >= MeteorSparkAppearZoom)
                            meteorSparkFrameCounter = 0;
                    }

                    for (int i = 0; i < 2; i++)
                    {
                        if (meteorSparkFrameCounter != -1)
                            meteorSparks[i].CurrentFrame = (uint)meteorSparkFrameCounter / 4;

                        meteorSparks[i].Visible = meteorSparkFrameCounter != -1;
                    }

                    ProcessTicks(elapsed);

                    if (currentZoom >= FadeOutZoom)
                    {
                        if (fadeOutTicks == -1)
                        {
                            fadeOutTicks = ticks;
                            black.Color = Color.Transparent;
                        }

                        long elapsedFadeTicks = ticks - fadeOutTicks;

                        black.DisplayLayer = 255;
                        black.Visible = true;
                        if (elapsedFadeTicks >= 1)
                            black.Color = new Color(0, (byte)Math.Min(255, (ticks - fadeOutTicks + 1) * 15 / 2)); // every odd tick
                        meteorGlowTarget = -1; // stop meteor glowing

                        if (black.Color.A >= 210)
                            zoomWaitCounter = 0; // stop zooming

                        if (black.Color.A == 255 && finishHandler != null)
                        {
                            finishHandler();
                            finishHandler = null;
                        }
                    }

                    // Meteor glowing
                    if (meteorGlowTarget != -1)
                    {
                        long glowTicks = (ticks - lastGlowTicks) / 4;
                        lastGlowTicks += glowTicks * 4;

                        for (int t = 0; t < glowTicks; t++)
                        {
                            if (glowingMeteorOverlay.Alpha < meteorGlowTarget)
                            {
                                int add = 255 / numGlowFadeIncrements;

                                if (glowingMeteorOverlay.Alpha + add >= meteorGlowTarget)
                                {
                                    glowingMeteorOverlay.Alpha = (byte)meteorGlowTarget;
                                    meteorGlowTarget = 0;
                                }
                                else
                                {
                                    glowingMeteorOverlay.Alpha = (byte)Math.Min(255, glowingMeteorOverlay.Alpha + add);
                                }
                            }
                            else if (glowingMeteorOverlay.Alpha > meteorGlowTarget)
                            {
                                int add = -255 / numGlowFadeIncrements;

                                if (glowingMeteorOverlay.Alpha + add <= meteorGlowTarget)
                                {
                                    glowingMeteorOverlay.Alpha = (byte)meteorGlowTarget;
                                    meteorGlowTarget = 255;
                                }
                                else
                                {
                                    glowingMeteorOverlay.Alpha = (byte)Math.Max(0, glowingMeteorOverlay.Alpha + add);
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var obj in objects)
                    {
                        obj.Visible = false;
                    }

                    meteorSparks[0].Visible = false;
                    meteorSparks[1].Visible = false;

                    if (glowingMeteorOverlay != null)
                        glowingMeteorOverlay.Visible = false;
                }
            }

            public override void Destroy()
            {
                foreach (var obj in objects)
                    obj?.Delete();

                foreach (var spark in meteorSparks)
                    spark?.Delete();

                town?.Delete();
                townText?.Destroy();
                glowingMeteorOverlay?.Delete();
                black?.Delete();
            }

            private void CheckTownDisplay(long ticks)
            {
                if (currentTownIndex == 3)
                    return;

                int nextTownIndex = currentTownIndex + 1;

                if (nextTownIndex < 3 && currentZoom >= TownShowInfos[nextTownIndex].ZoomLevel)
                {
                    currentTownIndex = nextTownIndex;
                    currentTownStartTicks = ticks;
                    town.X = 80;
                    town.Y = 36;
                    town.TextureAtlasOffset = textureAtlas.GetOffset((uint)IntroGraphic.Gemstone + (uint)currentTownIndex);
                    town.Visible = true;
                    black.Visible = true;
                    CreateTownText(currentTownIndex);
                }
                else if (currentTownIndex >= 0)
                {
                    town.Visible = TownShowInfos[currentTownIndex].Duration + 1 > (ticks - currentTownStartTicks);
                    black.Visible = town.Visible || currentZoom >= FadeOutZoom;

                    if (nextTownIndex == 3 && !town.Visible)
                        currentTownIndex = 3;

                    if (!town.Visible)
                    {
                        for (int i = 0; i < objects.Length; i++)
                        {
                            if (i != SunObjectIndex)
                                objects[i].Visible = true;
                        }

                        townText?.Destroy();
                        townText = null;
                    }
                }
            }

            private void ProcessTicks(long ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    if (!town.Visible)
                    {
                        if (zoomWaitCounter != 0)
                        {
                            if (zoomWaitCounter < 0)
                            {
                                var zoomTransition = ZoomTransitionInfos[zoomTransitionInfoIndex];

                                while (currentZoom >= zoomTransition.MaxZoom && zoomTransitionInfoIndex < 15)
                                {
                                    zoomTransition = ZoomTransitionInfos[++zoomTransitionInfoIndex];
                                }

                                if (zoomTransition.Increase == 0)
                                {
                                    zoomWaitCounter = 150; // Added offset to improve timing
                                    ++zoomTransitionInfoIndex;
                                }
                                else
                                {
                                    currentZoom = Math.Min(currentZoom + zoomTransition.Increase, MaxZoom);
                                }
                            }
                            else
                            {
                                if (--zoomWaitCounter == 0)
                                    zoomWaitCounter = -1;
                            }
                        }

                        if (meteorSparkFrameCounter != -1)
                        {
                            if (++meteorSparkFrameCounter == 60)
                                meteorSparkFrameCounter = 28;
                        }

                        // Zoom / Move
                        for (int n = 0; n < 5; n++)
                        {
                            if (n == MeteorObjectIndex && currentZoom >= MeteorEndZoom)
                                continue;

                            var obj = objects[n];
                            var info = ZoomInfos[n];
                            int distance = info.InitialDistance - currentZoom;

                            if (distance <= short.MaxValue)
                            {
                                distance += 256;

                                if (distance > 0 && distance <= short.MaxValue)
                                {
                                    int offsetX = info.EndOffsetX * 256;
                                    int offsetY = info.EndOffsetY * 256;
                                    offsetX /= distance;
                                    offsetY /= distance;
                                    offsetX += 160;
                                    offsetY = 100 - offsetY;

                                    int width = info.ZoomToWidth * 256;
                                    int height = info.ZoomToHeight * 256;
                                    width /= distance;
                                    height /= distance;

                                    offsetX -= width / 2;
                                    offsetY -= height / 2;

                                    obj.Resize(width, height);
                                    obj.X = offsetX;
                                    obj.Y = offsetY;
                                    obj.Visible = width >= 1 && height >= 1;

                                    if (n == MeteorObjectIndex && glowingMeteorOverlay != null)
                                    {
                                        glowingMeteorOverlay.X = obj.X;
                                        glowingMeteorOverlay.Y = obj.Y;
                                        glowingMeteorOverlay.Resize(width, height);
                                    }
                                }
                                else if (n != 0)
                                {
                                    obj.Visible = false; // Hide objects beside Lyramion
                                }
                            }
                        }
                    }
                }
            }
        }

        private class IntroActionTwinlake : IntroAction
        {
            private readonly IRenderView renderView;
            private readonly ITextureAtlas textureAtlas;
            private readonly long startTicks;
            private readonly ILayerSprite frame;
            private readonly ILayerSprite[] images = new ILayerSprite[95];
            private readonly IColoredRect black;
            private int activeFrame = -1;
            private Action finishHandler; // TODO

            public IntroActionTwinlake(IRenderView renderView, long startTicks, IIntroData introData, Action finishHandler)
                : base(IntroActionType.TwinlakeAnimation)
            {
                this.renderView = renderView;
                this.startTicks = startTicks;
                var layer = renderView.GetLayer(Layer.IntroGraphics);
                textureAtlas = TextureAtlasManager.Instance.GetOrCreate(Layer.IntroGraphics);
                byte paletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[IntroGraphic.Twinlake] - 1);
                uint partAtlasOffset = (uint)introData.Graphics.Keys.Max();

                frame = renderView.SpriteFactory.Create(288, 200, true, 0) as ILayerSprite;
                frame.Layer = layer;
                frame.TextureAtlasOffset = textureAtlas.GetOffset((uint)IntroGraphic.Frame);
                frame.PaletteIndex = (byte)(renderView.GraphicProvider.FirstIntroPaletteIndex + IntroData.GraphicPalettes[IntroGraphic.Frame] - 1);
                frame.X = 16;
                frame.Y = 0;
                frame.Visible = true;

                black = renderView.ColoredRectFactory.Create(320, 200, Color.Black, 255);
                black.Layer = renderView.GetLayer(Layer.IntroEffects);
                black.X = 0;
                black.Y = 0;
                black.Visible = true;

                images[0] = renderView.SpriteFactory.Create(256, 177, true, 20) as ILayerSprite;
                images[0].Layer = layer;
                images[0].TextureAtlasOffset = textureAtlas.GetOffset((uint)IntroGraphic.Twinlake);
                images[0].PaletteIndex = paletteIndex;
                images[0].X = 32;
                images[0].Y = 7;
                images[0].Visible = true;

                for (int i = 1; i < 95; i++)
                {
                    var twinlakePart = introData.TwinlakeImageParts[i - 1];
                    var graphic = twinlakePart.Graphic;
                    images[i] = renderView.SpriteFactory.Create(graphic.Width, graphic.Height, true, (byte)(50 + i * 2)) as ILayerSprite;
                    images[i].Layer = layer;
                    images[i].TextureAtlasOffset = textureAtlas.GetOffset(++partAtlasOffset);
                    images[i].PaletteIndex = paletteIndex;
                    images[i].X = 32 + twinlakePart.Position.X;
                    images[i].Y = 7 + twinlakePart.Position.Y;
                    images[i].Visible = false;
                }

                this.finishHandler = finishHandler;
            }

            public override void Destroy()
            {
                frame.Delete();
                black.Delete();

                foreach (var image in images)
                    image?.Delete();
            }

            public override void Update(long ticks, int frameCounter)
            {
                long elapsed = ticks - startTicks;

                black.Color = new Color(0, (byte)Math.Max(0, 255 - elapsed * 8));

                if (elapsed >= 250)
                {
                    elapsed -= 250;
                    int frame = (int)(elapsed / 4);

                    if (frame < 94)
                    {
                        if (frame != activeFrame)
                        {
                            /*if (activeFrame != -1)
                                images[activeFrame + 1].Visible = false;*/

                            images[frame + 1].Visible = true;
                        }
                    }
                }
            }
        }

        readonly Action finishAction;
        readonly IIntroData introData;
        readonly Font introFont;
        readonly Font introFontLarge;
        readonly IRenderView renderView;
        readonly IRenderLayer renderLayer; // TODO: needed?
        long ticks = 0;
        readonly List<Text> texts = new(); // TODO: needed?
        readonly List<IntroAction> actions = new();
        readonly IColoredRect fadeArea; // TODO: needed?
        Action fadeMidAction = null; // TODO: needed?
        long fadeStartTicks = 0;
        const long HalfFadeDurationInTicks = 3 * Game.TicksPerSecond / 4;
        const double TicksPerSecond = 60.0; // or test with 50 if not ok
        int animationFrameCounter = 0;
        readonly Queue<KeyValuePair<long, Func<IntroAction>>> actionQueue = new();
        readonly Queue<KeyValuePair<long, Action>> simpleActionQueue = new();
        ushort randomSeed = 0x0011;
        Action startMusicAction;
        double time = 0.0;

        private short Random()
        {
            long next = (long)randomSeed * 0xd117;
            randomSeed = (ushort)(next & 0xffff);
            return (short)((next >> 8) & 0x7fff);
        }

        // TODO: REMOVE?
        private static int GetPaletteFadeDuration(Graphic palette, int numColors, int ticksPerColorChange, int paletteOffset = 0)
        {
            int colorChanges = 0;

            for (int i = 0; i < numColors; ++i)
            {
                int r = palette.Data[(paletteOffset + i) * 4 + 0] >> 4;
                int g = palette.Data[(paletteOffset + i) * 4 + 1] >> 4;
                int b = palette.Data[(paletteOffset + i) * 4 + 2] >> 4;

                int dist = Util.Max(r, g, b);

                if (dist > colorChanges)
                {
                    colorChanges = dist; // this can be at max 15

                    if (colorChanges == 15)
                        break;
                }
            }

            return colorChanges * ticksPerColorChange;
        }

        private static int GetPaletteFadeDuration(Graphic palette1, int paletteOffset1, Graphic palette2, int paletteOffset2, int numColors, int ticksPerColorChange)
        {
            int colorChanges = 0;

            for (int i = 0; i < numColors; ++i)
            {
                int lr = palette1.Data[(paletteOffset1 + i) * 4 + 0] >> 4;
                int rr = palette2.Data[(paletteOffset2 + i) * 4 + 0] >> 4;
                int lg = palette1.Data[(paletteOffset1 + i) * 4 + 1] >> 4;
                int rg = palette2.Data[(paletteOffset2 + i) * 4 + 1] >> 4;
                int lb = palette1.Data[(paletteOffset1 + i) * 4 + 2] >> 4;
                int rb = palette2.Data[(paletteOffset2 + i) * 4 + 2] >> 4;

                int dist = Util.Max(Math.Abs(rr - lr), Math.Abs(rg - lg), Math.Abs(rb - lb));

                if (dist > colorChanges)
                {
                    colorChanges = dist; // this can be at max 15

                    if (colorChanges == 15)
                        break;
                }
            }

            return colorChanges * ticksPerColorChange;
        }

        public Intro(IRenderView renderView, IIntroData introData, Font introFont, Font introFontLarge, Action finishAction, Action startMusicAction)
        {
            this.finishAction = finishAction;
            this.introData = introData;
            this.introFont = introFont;
            this.introFontLarge = introFontLarge;
            this.renderView = renderView;
            this.startMusicAction = startMusicAction;
            renderLayer = renderView.GetLayer(Layer.IntroGraphics);

            fadeArea = renderView.ColoredRectFactory.Create(Global.VirtualScreenWidth, Global.VirtualScreenHeight, Color.Black, 255);
            fadeArea.Layer = renderView.GetLayer(Layer.Effects);
            fadeArea.X = 0;
            fadeArea.Y = 0;
            fadeArea.Visible = false;

            ScheduleAction(0, IntroActionType.Starfield);
            // Between the starfield setup and Thalion logo, the palette
            // is faded which takes 15 color changes with 4 ticks per change.
            ScheduleAction(15 * 4, IntroActionType.ThalionLogoFlyIn, () =>
            {
                DestroyAction(IntroActionType.ThalionLogoFlyIn);

                ScheduleAction(ticks, IntroActionType.AmbermoonFlyIn, () =>
                {
                    DestroyAction(IntroActionType.AmbermoonFlyIn);

                    // It seems we might be 1 tick off and this will affect the static star image.
                    // Try to use 812 if possible. This way we always show the same stars in the background (hopefully).
                    const long baseTicks = 812;

                    // After Ambermoon logo there is a delay of 150 ticks.
                    // But this includes the 60 ticks for fading out.
                    // There are 3 more ticks in-between.
                    // So in total 93 ticks.
                    int delay = 93;

                    // Stop the starfield animation at exaclty the right ticks
                    (actions.First(a => a.Type == IntroActionType.Starfield) as IntroActionStarfield)!.StopAt(baseTicks + delay);
                    // Planets and texts appear 20 ticks later
                    delay += 20;
                    ScheduleAction(baseTicks + delay, IntroActionType.TextCommands);
                    ScheduleAction(baseTicks + delay, IntroActionType.DisplayObjects, () =>
                    {
                        DestroyAction(IntroActionType.Starfield);
                        DestroyAction(IntroActionType.TextCommands);
                        DestroyAction(IntroActionType.DisplayObjects);
                        ScheduleAction(ticks, IntroActionType.TwinlakeAnimation);
                    });
                });
            });            
        }

        private void DestroyAction(IntroActionType actionType)
        {
            foreach (var action in actions.Where(a => a.Type == actionType).ToArray())
            {
                action.Destroy();
                actions.Remove(action);
            }
        }

        private void ScheduleAction(long startTicks, IntroActionType actionType, Action finishHandler = null, Action additionalAction = null)
        {
            var adder = () =>
            {
                additionalAction?.Invoke();
                return IntroAction.CreateAction(actionType, this, finishHandler, renderView, startTicks, introData, Random, introFont, introFontLarge);
            };

            actionQueue.Enqueue(KeyValuePair.Create(startTicks, adder));
        }

        private void ScheduleAction(long startTicks, Action action)
        {
            simpleActionQueue.Enqueue(KeyValuePair.Create(startTicks, action));
        }

        internal void StartMeteorGlowing(long ticks)
        {
            (actions.FirstOrDefault(a => a.Type == IntroActionType.DisplayObjects) as IntroActionDisplayObjects)?.StartMeteorGlowing(ticks);
        }

        public void Update(double deltaTime)
        {
            double lastTime = time;
            time += deltaTime * 5.0 / 6.0; // We have to match the music and it runs with 50Hz while the update cycle runs with 60Hz.
            long oldTicks = (long)Math.Round(TicksPerSecond * lastTime);
            ticks = (long)Math.Round(TicksPerSecond * time);

            if (startMusicAction != null && ticks >= 1)
            {
                // Music starts after 1 tick
                startMusicAction?.Invoke();
                startMusicAction = null;
            }

            while (simpleActionQueue.Count > 0)
            {
                if (simpleActionQueue.Peek().Key <= ticks)
                    simpleActionQueue.Dequeue().Value();
                else
                    break;
            }

            while (actionQueue.Count > 0)
            {
                if (actionQueue.Peek().Key <= ticks)
                    actions.Add(actionQueue.Dequeue().Value());
                else
                    break;
            }

            animationFrameCounter = (int)((animationFrameCounter + (ticks - oldTicks)) % 48);

            if (fadeArea.Visible || fadeMidAction != null)
            {
                long fadeDuration = ticks - fadeStartTicks;

                if (fadeDuration >= HalfFadeDurationInTicks * 2)
                {
                    fadeMidAction = null;
                    fadeArea.Visible = false;
                }
                else
                {
                    byte alpha = (byte)(255 - Math.Abs(HalfFadeDurationInTicks - fadeDuration) * 255 / HalfFadeDurationInTicks);
                    fadeArea.Color = new Render.Color(fadeArea.Color, alpha);
                    fadeArea.Visible = true;

                    if (fadeMidAction != null && fadeDuration >= HalfFadeDurationInTicks)
                    {
                        fadeMidAction?.Invoke();
                        fadeMidAction = null;
                    }

                    return;
                }
            }

            foreach (var action in actions.ToArray())
            {
                action.Update(ticks, animationFrameCounter);
            }
        }

        public void Click()
        {
            End();
        }

        void End()
        {
            Destroy();
            finishAction?.Invoke();
        }

        void Fade(Action midAction)
        {
            fadeStartTicks = ticks;
            fadeMidAction = midAction;
        }

        void PrintText(int x, int y, string text, bool large)
        {
            Text textEntry;

            if (large)
            {
                textEntry = introFontLarge.CreateText(renderView, Layer.IntroText,
                    new Rect(x, y, Global.VirtualScreenWidth - x, 22), text, 10, TextAlign.Left, 208);
            }
            else
            {
                textEntry = introFont.CreateText(renderView, Layer.IntroText,
                    new Rect(x, y, Global.VirtualScreenWidth - x, 11), text, 10, TextAlign.Left, 208);
            }

            textEntry.Visible = true;

            texts.Add(textEntry);
        }

        public void Destroy()
        {
            actions.ForEach(action => action.Destroy());
            actions.Clear();
            texts.ForEach(text => text.Destroy());
            texts.Clear();
            fadeArea.Delete();
        }
    }
}
