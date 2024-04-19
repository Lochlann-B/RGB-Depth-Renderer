using System.Diagnostics;
using System.Runtime.InteropServices;

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

    private int _pboId;
    
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
    public Texture(float[,] texValues)
    {
        _handle = GL.GenTexture();
        _pboId = GL.GenBuffer();
        
        _width = texValues.GetLength(1);
        _height = texValues.GetLength(0);
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, texValues.GetLength(1),
            texValues.GetLength(0));
        // GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, texValues.GetLength(1), texValues.GetLength(0),
        //     PixelFormat.Red, PixelType.Float, IntPtr.Zero);#
        GL.TexImage2D(TextureTarget.Texture2D, 
            0, 
            PixelInternalFormat.R32f, 
            _width, 
            _height, 
            0, 
            PixelFormat.Red, 
            PixelType.Float, 
            IntPtr.Zero);
        
        
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pboId);
        GL.BufferData(BufferTarget.PixelUnpackBuffer, _width * _height * 4, IntPtr.Zero, BufferUsageHint.StreamDraw); // Assuming RGBA
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); // Unbind PBO
        
        // GL.BindTexture(TextureTarget.Texture2D, 0);
        UpdateWithFloatArrayData(texValues);
    }

    public Texture(byte[] data, int width, int height)
    {
        _handle = GL.GenTexture();
        
        _pboId = GL.GenBuffer();
        
        _width = width;
        _height = height;
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        
        StbImage.stbi_set_flip_vertically_on_load(1);
        
        //GL.BindTexture(TextureTarget.Texture2D, _handle);
        //GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        
        GL.TexImage2D(TextureTarget.Texture2D, 
            0, 
            PixelInternalFormat.Rgba, 
            _width, 
            _height, 
            0, 
            PixelFormat.Rgba, 
            PixelType.UnsignedByte, 
            IntPtr.Zero);
        
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pboId);
        GL.BufferData(BufferTarget.PixelUnpackBuffer, width * height * 4, IntPtr.Zero, BufferUsageHint.StreamDraw); // Assuming RGBA
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); // Unbind PBO
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        
        // GL.BindTexture(TextureTarget.Texture2D, 0);
        
        UpdateWithByteData(data);
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

    public unsafe void UpdateWithByteData(byte[] data)
    {
        fixed (byte* p = data)
        {
            var pter = (IntPtr)p;
            
            GL.BindTexture(TextureTarget.Texture2D, _handle);
            // watch.Stop();
            // Console.WriteLine("bind time: {0}ms", watch.ElapsedMilliseconds);
            // watch.Restart();
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _width, _height, PixelFormat.Rgba, PixelType.UnsignedByte, pter);
            // watch.Stop();
        }
        
        return;
        
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pboId);

        // Map the buffer object into client's memory
        IntPtr ptr = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly);
        
        // System.Buffer.BlockCopy(data, 0, ptr, 0, data.Length);
        GL.BufferData(BufferTarget.PixelUnpackBuffer, data.Length, data, BufferUsageHint.StreamDraw);

        // Copy data to the PBO
        // var watch = Stopwatch.StartNew();
        // Marshal.Copy(data, 0, ptr, data.Length);
        // watch.Stop();
        // Console.WriteLine("marshal copy time: {0}ms", watch.ElapsedMilliseconds);

        // Unmap the buffer
        GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);

        
        
        // var watch = new Stopwatch();
        // watch.Start();
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        // watch.Stop();
        // Console.WriteLine("bind time: {0}ms", watch.ElapsedMilliseconds);
        // watch.Restart();
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _width, _height, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        // watch.Stop();
        // Console.WriteLine("time taken to upload texture: {0}ms", watch.ElapsedMilliseconds);
        
        // GL.TexImage2D(TextureTarget.Texture2D, 
        //     0, 
        //     PixelInternalFormat.Rgba, 
        //     _image.Width, 
        //     _image.Height, 
        //     0, 
        //     PixelFormat.Rgba, 
        //     PixelType.UnsignedByte, 
        //     data);
        
        // Unbind PBO and Texture
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public unsafe void UpdateWithFloatArrayData(float[,] data)
    {
        fixed (float* p = data)
        {
            var pter = (IntPtr)p;
            
            GL.BindTexture(TextureTarget.Texture2D, _handle);
            // watch.Stop();
            // Console.WriteLine("bind time: {0}ms", watch.ElapsedMilliseconds);
            // watch.Restart();
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _width, _height, PixelFormat.Red, PixelType.Float, pter);
            // watch.Stop();
        }
        
        return;
        
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pboId);

        // Map the buffer object into client's memory
        IntPtr ptr = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly);

        // Copy data to the PBO
        Marshal.Copy(Convert2DArrayTo1D(data), 0, ptr, data.Length);

        // Unmap the buffer
        GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);
        
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, data.GetLength(1), data.GetLength(0),
            PixelFormat.Red, PixelType.Float, IntPtr.Zero);
        
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
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
    
    public static float[] Convert2DArrayTo1D(float[,] twoDimensionalArray)
    {
        int rowCount = twoDimensionalArray.GetLength(0);
        int colCount = twoDimensionalArray.GetLength(1);
        float[] oneDimensionalArray = new float[rowCount * colCount];
        System.Buffer.BlockCopy(twoDimensionalArray, 0, oneDimensionalArray, 0, rowCount * colCount * sizeof(float));
        return oneDimensionalArray;
    }
}