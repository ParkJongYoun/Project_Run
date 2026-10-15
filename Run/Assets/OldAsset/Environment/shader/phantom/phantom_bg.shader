Shader "project-rs/phantom/bg_phantom"
{	
    SubShader
    {
		Tags { "Queue"="Geometry-1" }
		Pass
		{
			Stencil
			{
				Ref 11
				Comp Always
				Pass Replace
    		}						
		}		
    }    
}