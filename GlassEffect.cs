using Godot;
using System;

public class GlassEffect : ColorRect
{

	private ShaderMaterial _shader;
	
	public override void _Ready()
	{
		
		// Cast the Material property to ShaderMaterial
		_shader = (ShaderMaterial)this.Material;
	}

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		// Set the shader time uniform manually
		if (_shader != null)
		{
			_shader.SetShaderParam("time", OS.GetTicksMsec() / 1000.0f);
		}
	}
}
