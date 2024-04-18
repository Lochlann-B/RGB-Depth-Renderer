using System.Diagnostics;

namespace RGBDReconstruction.Application;

using System;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

public class Texture
{
    private int _handle;
    private ImageResult _image;
    private int _width;
    private int _height;
    
    public Texture(string imgPath)
    {
        _handle = GL.GenTexture();
        
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
        StbImage.stbi_set_flip_vertically_on_load(1);
 
        using (var img = File.OpenRead(imgPath))
        {
            _image = ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha);
            GL.TexImage2D(TextureTarget.Texture2D, 
                0, 
                PixelInternalFormat.Rgba, 
                _image.Width, 
                _image.Height, 
                0, 
                PixelFormat.Rgba, 
                PixelType.UnsignedByte, 
                _image.Data);
        }

        _width = _image.Width;
        _height = _image.Height;
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void UpdateTexture(string newImgPath)
    {
        var watch = new Stopwatch();
        watch.Start();
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
        StbImage.stbi_set_flip_vertically_on_load(1);
        //TODO: Change to using{} block so that img file gets GC'd after this function
        using (var img = File.OpenRead(newImgPath))
        {
            _image = ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha);
            watch.Stop();
            Console.WriteLine("Time taken to load image: {0}ms", watch.ElapsedMilliseconds);
            watch.Reset();
            watch.Start();
            GL.TexImage2D(TextureTarget.Texture2D, 
                0, 
                PixelInternalFormat.Rgba, 
                _image.Width, 
                _image.Height, 
                0, 
                PixelFormat.Rgba, 
                PixelType.UnsignedByte, 
                _image.Data);
            watch.Stop();
            Console.WriteLine("Time taken to load image data into OpenGL: {0}ms", watch.ElapsedMilliseconds);
            watch.Reset();
        }
        
        
        
    }

    public void UpdateWithByteData(byte[] data)
    {
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _width, _height, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        // GL.TexImage2D(TextureTarget.Texture2D, 
        //     0, 
        //     PixelInternalFormat.Rgba, 
        //     _image.Width, 
        //     _image.Height, 
        //     0, 
        //     PixelFormat.Rgba, 
        //     PixelType.UnsignedByte, 
        //     data);
    }

    public void UpdateWithFloatArrayData(float[,] data)
    {
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, data.GetLength(1), data.GetLength(0),
            PixelFormat.Red, PixelType.Float, data);
    }

    public Texture(float[,] texValues)
    {
        _handle = GL.GenTexture();
        _width = texValues.GetLength(1);
        _height = texValues.GetLength(0);
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, texValues.GetLength(1),
            texValues.GetLength(0));
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, texValues.GetLength(1), texValues.GetLength(0),
            PixelFormat.Red, PixelType.Float, texValues);
    }

    public Texture(byte[] data, int width, int height)
    {
        _handle = GL.GenTexture();
        _width = width;
        _height = height;
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
        StbImage.stbi_set_flip_vertically_on_load(1);
        
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, data);
    }

    public Texture()
    {
        _handle = GL.GenTexture();
    }

    public void Use(TextureUnit texTarget = TextureUnit.Texture0)
    {
        GL.ActiveTexture(texTarget);
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
    }

    public void SetLocation(int loc)
    {
        GL.Uniform1(_handle, loc);
    }
}