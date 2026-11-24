using UnityEngine;

namespace CampusRun.Auth
{
    /// <summary>
    /// 1, 2, 3위 달성 보상 테두리 장식(금/은/동)의 메타데이터, 색상,
    /// 도트 아케이드 9-Slice 테두리 프레임 스프라이트 및 16x16 도트 아이콘 스프라이트를 생성·캐싱하는 유틸리티입니다.
    /// </summary>
    public static class BorderDecorationUtility
    {
        private static readonly Sprite[] CachedFrameSprites = new Sprite[4];
        private static readonly Sprite[] CachedIconSprites = new Sprite[4];

        public static string GetDisplayName(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return "황금 왕관 테두리";
                case BorderTier.Silver: return "백은 기사 테두리";
                case BorderTier.Bronze: return "청동 러너 테두리";
                default: return "기본 (장착 해제)";
            }
        }

        public static string GetShortBadgeName(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return "1위 금테두리";
                case BorderTier.Silver: return "2위 은테두리";
                case BorderTier.Bronze: return "3위 동테두리";
                default: return "기본 테두리";
            }
        }

        public static string GetUnlockConditionText(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return "전체 랭킹 1위 달성 시 영구 획득";
                case BorderTier.Silver: return "전체 랭킹 2위 달성 시 영구 획득";
                case BorderTier.Bronze: return "전체 랭킹 3위 달성 시 영구 획득";
                default: return "기본 제공";
            }
        }

        public static int GetRequiredRank(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return 1;
                case BorderTier.Silver: return 2;
                case BorderTier.Bronze: return 3;
                default: return 0;
            }
        }

        public static Color GetPrimaryColor(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return new Color(1f, 0.84f, 0.16f, 1f);
                case BorderTier.Silver: return new Color(0.82f, 0.90f, 1f, 1f);
                case BorderTier.Bronze: return new Color(0.90f, 0.56f, 0.28f, 1f);
                default: return new Color(0.32f, 0.82f, 0.52f, 1f);
            }
        }

        public static Color GetAccentBgColor(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return new Color(0.22f, 0.16f, 0.04f, 0.94f);
                case BorderTier.Silver: return new Color(0.11f, 0.15f, 0.23f, 0.94f);
                case BorderTier.Bronze: return new Color(0.20f, 0.11f, 0.06f, 0.94f);
                default: return new Color(0.08f, 0.11f, 0.18f, 0.92f);
            }
        }

        public static string GetHexColor(BorderTier tier)
        {
            switch (tier)
            {
                case BorderTier.Gold: return "#FFD700";
                case BorderTier.Silver: return "#D2E6FF";
                case BorderTier.Bronze: return "#E69148";
                default: return "#4ADE80";
            }
        }

        /// <summary>
        /// 등급별 도트 아케이드 9-Slice 테두리 프레임(중앙 투명, 외곽 베벨 + 모서리 보석 장식)을 반환합니다.
        /// </summary>
        public static Sprite GetBorderFrameSprite(BorderTier tier)
        {
            int idx = Mathf.Clamp((int)tier, 0, 3);
            if (CachedFrameSprites[idx] != null) return CachedFrameSprites[idx];

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color outerOutline;
            Color highlight;
            Color midTone;
            Color shadowTone;
            Color gemColor;
            Color gemShine;

            switch (tier)
            {
                case BorderTier.Gold:
                    outerOutline = new Color(0.22f, 0.11f, 0.02f, 1f);
                    highlight = new Color(1f, 0.97f, 0.62f, 1f);
                    midTone = new Color(1f, 0.80f, 0.12f, 1f);
                    shadowTone = new Color(0.74f, 0.44f, 0.04f, 1f);
                    gemColor = new Color(1f, 0.25f, 0.28f, 1f);     // 루비 포인트
                    gemShine = new Color(1f, 0.92f, 0.92f, 1f);
                    break;
                case BorderTier.Silver:
                    outerOutline = new Color(0.10f, 0.15f, 0.24f, 1f);
                    highlight = new Color(0.95f, 0.99f, 1f, 1f);
                    midTone = new Color(0.74f, 0.84f, 0.95f, 1f);
                    shadowTone = new Color(0.42f, 0.52f, 0.68f, 1f);
                    gemColor = new Color(0.24f, 0.72f, 1f, 1f);     // 사파이어 포인트
                    gemShine = new Color(0.90f, 0.98f, 1f, 1f);
                    break;
                case BorderTier.Bronze:
                    outerOutline = new Color(0.18f, 0.08f, 0.03f, 1f);
                    highlight = new Color(1f, 0.80f, 0.56f, 1f);
                    midTone = new Color(0.84f, 0.49f, 0.22f, 1f);
                    shadowTone = new Color(0.54f, 0.27f, 0.09f, 1f);
                    gemColor = new Color(0.25f, 0.86f, 0.52f, 1f);  // 에메랄드 포인트
                    gemShine = new Color(0.85f, 1f, 0.90f, 1f);
                    break;
                default:
                    outerOutline = new Color(0.06f, 0.12f, 0.14f, 1f);
                    highlight = new Color(0.42f, 0.92f, 0.60f, 1f);
                    midTone = new Color(0.24f, 0.72f, 0.44f, 1f);
                    shadowTone = new Color(0.14f, 0.44f, 0.28f, 1f);
                    gemColor = midTone;
                    gemShine = highlight;
                    break;
            }

            bool isDecorated = tier != BorderTier.None;
            int thickness = isDecorated ? 5 : 3;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);
                    int dist = Mathf.Min(dx, dy);

                    // 바깥 모서리 1픽셀 컷 (도트 라운딩)
                    if (dx == 0 && dy == 0)
                    {
                        tex.SetPixel(x, y, clear);
                        continue;
                    }

                    // 장식 등급 모서리 보석/픽셀 장식 (4x4 코너 엠블럼)
                    if (isDecorated && dx <= 5 && dy <= 5)
                    {
                        if (dx == 0 || dy == 0 || (dx == 5 && dy <= 5) || (dy == 5 && dx <= 5))
                        {
                            // 코너 외곽 및 내곽 라인
                            if (dx == 0 || dy == 0)
                            {
                                tex.SetPixel(x, y, outerOutline);
                                continue;
                            }
                            if ((dx == 5 && dy == 5))
                            {
                                tex.SetPixel(x, y, outerOutline);
                                continue;
                            }
                        }

                        if (dx >= 2 && dx <= 3 && dy >= 2 && dy <= 3)
                        {
                            // 중앙 보석 픽셀
                            bool isTopLeftGem = (dx == 2 && dy == 3);
                            tex.SetPixel(x, y, isTopLeftGem ? gemShine : gemColor);
                            continue;
                        }

                        if (dx == 1 || dy == 1)
                        {
                            tex.SetPixel(x, y, highlight);
                            continue;
                        }

                        if (dx == 4 || dy == 4)
                        {
                            tex.SetPixel(x, y, shadowTone);
                            continue;
                        }
                    }

                    // 내부 투명 영역
                    if (dist >= thickness)
                    {
                        tex.SetPixel(x, y, clear);
                        continue;
                    }

                    // 최외곽 & 최내곽 1픽셀 짙은 도트 아웃라인
                    if (dist == 0 || dist == thickness - 1)
                    {
                        tex.SetPixel(x, y, outerOutline);
                        continue;
                    }

                    // 중간 베벨 음영 (상단/좌측 하이라이트, 하단/우측 그림자)
                    if (isDecorated)
                    {
                        if (dist == 1)
                        {
                            bool isLitSide = (y >= size - 4 || x <= 3);
                            tex.SetPixel(x, y, isLitSide ? highlight : midTone);
                        }
                        else if (dist == 2)
                        {
                            // 미세 반짝임 픽셀 패턴
                            bool sparkle = ((x + y) % 6 == 0);
                            tex.SetPixel(x, y, sparkle ? highlight : midTone);
                        }
                        else
                        {
                            bool isShadowSide = (y <= 4 || x >= size - 5);
                            tex.SetPixel(x, y, isShadowSide ? shadowTone : midTone);
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, (y >= size - 3 || x <= 2) ? highlight : midTone);
                    }
                }
            }

            tex.Apply();
            float borderSlice = isDecorated ? 8f : 5f;
            CachedFrameSprites[idx] = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(borderSlice, borderSlice, borderSlice, borderSlice));

            return CachedFrameSprites[idx];
        }

        /// <summary>
        /// 등급별 16x16 도트 아이콘 스프라이트(금=왕관, 은=은메달, 동=동메달, 없음=기본 배지)를 반환합니다.
        /// </summary>
        public static Sprite GetBorderIconSprite(BorderTier tier)
        {
            int idx = Mathf.Clamp((int)tier, 0, 3);
            if (CachedIconSprites[idx] != null) return CachedIconSprites[idx];

            string[] pattern;
            Color outline = new Color(0.14f, 0.08f, 0.04f, 1f);
            Color mainCol;
            Color shineCol;
            Color shadeCol;
            Color gemCol;

            if (tier == BorderTier.Gold)
            {
                // 황금 왕관 16x16
                pattern = new string[]
                {
                    "................",
                    ".......OO.......",
                    "..OO..OWYO..OO..",
                    ".OWYO.OYYO.OYYO.",
                    ".OYYO.OYYO.OYYO.",
                    ".OYYOOYYYYOOYYO.",
                    ".OYYYYYYYYYYYYO.",
                    ".OYYGYYGGYYGYYO.",
                    ".OYYYYYYYYYYYYO.",
                    ".OSSSSSSSSSSSSO.",
                    ".OOYYYYYYYYYYOO.",
                    "..OYYYYWWYYYYO..",
                    "..OSSSSSSSSSSO..",
                    "..OOOOOOOOOOOO..",
                    "................",
                    "................"
                };
                mainCol = new Color(1f, 0.84f, 0.15f, 1f);
                shineCol = new Color(1f, 0.98f, 0.78f, 1f);
                shadeCol = new Color(0.80f, 0.48f, 0.06f, 1f);
                gemCol = new Color(1f, 0.25f, 0.28f, 1f);
            }
            else if (tier == BorderTier.Silver)
            {
                // 백은 메달 16x16
                pattern = new string[]
                {
                    "....OOOOOOOO....",
                    "...OGGOGGOGGO...",
                    "....OGGOGGGO....",
                    ".....OOGGOO.....",
                    "....OOYYYYOO....",
                    "...OYYWWWWYYO...",
                    "..OYYWYYYYYYYO..",
                    "..OYYYYGGYYYYO..",
                    "..OYYYGGGGYYYO..",
                    "..OYYYYGGYYYYO..",
                    "..OYYYYYYYYSSO..",
                    "...OYYSSSSSSO...",
                    "....OOSSSSOO....",
                    ".....OOOOOO.....",
                    "................",
                    "................"
                };
                mainCol = new Color(0.80f, 0.88f, 0.98f, 1f);
                shineCol = new Color(0.97f, 0.99f, 1f, 1f);
                shadeCol = new Color(0.45f, 0.55f, 0.70f, 1f);
                gemCol = new Color(0.24f, 0.68f, 1f, 1f);
            }
            else if (tier == BorderTier.Bronze)
            {
                // 청동 메달 16x16
                pattern = new string[]
                {
                    "....OOOOOOOO....",
                    "...OGGOGGOGGO...",
                    "....OGGOGGGO....",
                    ".....OOGGOO.....",
                    "....OOYYYYOO....",
                    "...OYYWWWWYYO...",
                    "..OYYWYYYYYYYO..",
                    "..OYYYYYYYYYYO..",
                    "..OYYYWWWWYYYO..",
                    "..OYYYYYYYYYYO..",
                    "..OYYYYYYYYSSO..",
                    "...OYYSSSSSSO...",
                    "....OOSSSSOO....",
                    ".....OOOOOO.....",
                    "................",
                    "................"
                };
                mainCol = new Color(0.86f, 0.52f, 0.24f, 1f);
                shineCol = new Color(1f, 0.82f, 0.58f, 1f);
                shadeCol = new Color(0.55f, 0.28f, 0.10f, 1f);
                gemCol = new Color(0.22f, 0.78f, 0.45f, 1f);
            }
            else
            {
                // 기본 캠퍼스 러너 배지 16x16
                pattern = new string[]
                {
                    "................",
                    ".....OOOOOO.....",
                    "...OOYYYYYYOO...",
                    "..OYYWWWWYYYYO..",
                    "..OYYYYYYYYYYO..",
                    ".OYYYYOOOOYYYYO.",
                    ".OYYYOWWWWOYYYO.",
                    ".OYYYOWWWWOYYYO.",
                    ".OYYYYOOOOYYYYO.",
                    ".OYYOOOOOOOOYYO.",
                    "..OOWWWWWWWWOO..",
                    "..OSSSSSSSSSSO..",
                    "...OOSSSSSSOO...",
                    ".....OOOOOO.....",
                    "................",
                    "................"
                };
                mainCol = new Color(0.25f, 0.78f, 0.48f, 1f);
                shineCol = new Color(0.80f, 1f, 0.88f, 1f);
                shadeCol = new Color(0.14f, 0.46f, 0.30f, 1f);
                gemCol = mainCol;
            }

            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int row = 0; row < size; row++)
            {
                int y = size - 1 - row;
                string line = pattern[row];
                for (int x = 0; x < size; x++)
                {
                    char c = line[x];
                    Color col = clear;
                    if (c == 'O') col = outline;
                    else if (c == 'Y') col = mainCol;
                    else if (c == 'W') col = shineCol;
                    else if (c == 'S') col = shadeCol;
                    else if (c == 'G') col = gemCol;
                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            CachedIconSprites[idx] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return CachedIconSprites[idx];
        }
    }
}
