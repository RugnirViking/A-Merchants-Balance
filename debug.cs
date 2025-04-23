using Godot;
using System;

public class debug : Control  // build as a Control scene, not Node2D
{
	// --- same parameters as the shader ---------------------------
	const float  kStrength     = 0.3f;
	readonly     Vector2 kRect = new Vector2(0.8f, 0.8f);
	const float  kCorner       = 0.05f;

	Vector2 _mouseUv  = Vector2.Zero;   // SCREEN_UV where you clicked
	Vector2 _worldUv  = Vector2.Zero;   // undistorted UV we compute

	/* ===================== CLICK HANDLER ======================== */
	public override void _Input(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.ButtonIndex == (int)ButtonList.Left && mb.Pressed)
		{
			_mouseUv  = mb.Position / RectSize;          // what shader sees
			_worldUv  = InverseGlassUV(_mouseUv);        // our inverse
			Update();                                    // trigger _Draw
		}
	}

	/* ====================== DRAW TEST MARKERS =================== */
	public override void _Draw()
	{
		// screen point you clicked  (blue)
		DrawLine(_mouseUv * RectSize - new Vector2(6,0), _mouseUv * RectSize + new Vector2(6,0), Colors.Blue, 2);
		DrawLine(_mouseUv * RectSize - new Vector2(0,6), _mouseUv * RectSize + new Vector2(0,6), Colors.Blue, 2);

		// where inverse says that world point really is (red)
		DrawLine(_worldUv * RectSize - new Vector2(6,0), _worldUv * RectSize + new Vector2(6,0), Colors.Red, 2);
		DrawLine(_worldUv * RectSize - new Vector2(0,6), _worldUv * RectSize + new Vector2(0,6), Colors.Red, 2);
	}

	/* ================ EXACT INVERSE OF YOUR SHADER ============== */
	Vector2 InverseGlassUV(Vector2 uvFinal)
	{
		// 1-a.  pixel-based rounded-rect mask  --------------------
		Vector2 screenPx = RectSize;
		Vector2 centerPx = (uvFinal - new Vector2(0.5f,0.5f)) * screenPx;
		Vector2 halfPx   = kRect * screenPx * 0.5f;
		float   radPx    = kCorner * Mathf.Min(screenPx.x, screenPx.y);

		Vector2 d   = new Vector2(Mathf.Abs(centerPx.x), Mathf.Abs(centerPx.y)) - halfPx + new Vector2(radPx, radPx);
		float   dst = new Vector2(Mathf.Max(d.x,0), Mathf.Max(d.y,0)).Length() - radPx;
		if (dst > 0f) return uvFinal;                     // clicked outside → no distortion

		// 1-b. bring to pos' = uv*2-1
		Vector2 posP = uvFinal * 2f - Vector2.One;
		float   rp   = posP.Length();

		// 2. invert  rp = r (1 + k r²)  analytically  ---------------
		float r = InverseBarrel(rp, kStrength);
		float scale = (rp > 0f) ? r / rp : 0f;
		Vector2 pos = posP * scale;

		// 3. back to UV  -------------------------------------------
		return pos * 0.5f + new Vector2(0.5f, 0.5f);
	}

	/* --------- exact cubic inverse  r' = r (1 + k r²) ------------ */
	static float Cbrt(float x)
		=> x >= 0f ? Mathf.Pow(x, 1f/3f) : -Mathf.Pow(-x, 1f/3f);

	static float InverseBarrel(float rp, float k)
	{
		if (k == 0f) return rp;

		float p = 1f / k;          // depressed cubic
		float q = -rp / k;
		float Δ = 0.25f*q*q + (p*p*p)/27f;
		float s = Mathf.Sqrt(Δ);

		float u = Cbrt(-0.5f*q + s);
		float v = Cbrt(-0.5f*q - s);

		return Mathf.Clamp(u+v, 0f, 1f);
	}
}
