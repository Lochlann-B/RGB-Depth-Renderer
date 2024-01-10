namespace RGBDReconstruction.Application;

using System;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

public class Texture
{
    private int _handle;
    private ImageResult _image;
    
    public Texture(string imgPath)
    {
        _handle = GL.GenTexture();
        
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
        StbImage.stbi_set_flip_vertically_on_load(1);
        //TODO: Change to using{} block so that img file gets GC'd after this function
        _image = ImageResult.FromStream(File.OpenRead(imgPath), ColorComponents.RedGreenBlueAlpha);
        GL.TexImage2D(TextureTarget.Texture2D, 
            0, 
            PixelInternalFormat.Rgba, 
            _image.Width, 
            _image.Height, 
            0, 
            PixelFormat.Rgba, 
            PixelType.UnsignedByte, 
            _image.Data);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void Use(TextureUnit texTarget = TextureUnit.Texture0)
    {
        GL.ActiveTexture(texTarget);
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
    }
}