Shader "UI/GachaBorder"
{
    Properties
    {
        _MainTex    ("Texture",         2D)     = "white" {}
        _Color      ("Border Color",    Color)  = (0.3, 0.6, 1, 1)
        _BorderWidth("Border Width",    Range(0.01, 0.1)) = 0.025
        _Progress   ("Snake Progress",  Range(0, 1))      = 0
        _SnakeLen   ("Snake Length",    Range(0.01, 0.5)) = 0.2
        _GlowWidth  ("Glow Width",      Range(0.01, 0.3)) = 0.15
        _Brightness ("Glow Brightness", Range(1, 4))      = 2.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float  _BorderWidth;
            float  _Progress;
            float  _SnakeLen;
            float  _GlowWidth;
            float  _Brightness;
            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // 将 uv 坐标转换为沿边框顺时针的 0~1 进度
            // 顺序：上边(左→右) → 右边(上→下) → 下边(右→左) → 左边(下→上)
            float uvToPerimeter(float2 uv)
            {
                float x = uv.x, y = uv.y;
                float b = _BorderWidth;

                // 判断在哪条边
                bool onTop    = y > 1.0 - b;
                bool onBottom = y < b;
                bool onRight  = x > 1.0 - b;
                bool onLeft   = x < b;

                if (onTop)    return x * 0.25;                        // 上：0.00~0.25
                if (onRight)  return 0.25 + (1.0 - y) * 0.25;        // 右：0.25~0.50
                if (onBottom) return 0.50 + (1.0 - x) * 0.25;        // 下：0.50~0.75
                if (onLeft)   return 0.75 + y * 0.25;                 // 左：0.75~1.00
                return -1; // 内部
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float b = _BorderWidth;

                // 是否在边框区域
                bool inBorder = uv.x < b || uv.x > 1.0 - b ||
                                uv.y < b || uv.y > 1.0 - b;
                if (!inBorder) discard;

                float pos = uvToPerimeter(uv);
                if (pos < 0) discard;

                // 计算与蛇头的距离（循环）
                float diff = pos - _Progress;
                if (diff < -0.5) diff += 1.0;
                if (diff >  0.5) diff -= 1.0;

                // 蛇身：在 [-snakeLen, 0] 范围内
                float alpha = 0.0;
                fixed4 col  = _Color;

                if (diff <= 0.0 && diff >= -_SnakeLen)
                {
                    float t = 1.0 + diff / _SnakeLen; // 0(尾)~1(头)
                    // 蛇头发光
                    float glow = smoothstep(1.0 - _GlowWidth, 1.0, t);
                    col   = lerp(_Color, fixed4(1,1,1,1), glow * (_Brightness - 1.0) / _Brightness);
                    alpha = lerp(0.2, 1.0, t);
                }
                else
                {
                    // 暗边框底色
                    col   = _Color * 0.25;
                    alpha = 0.6;
                }

                return fixed4(col.rgb, alpha);
            }
            ENDCG
        }
    }
}
