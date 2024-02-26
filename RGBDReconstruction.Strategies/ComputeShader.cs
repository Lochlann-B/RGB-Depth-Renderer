using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RGBDReconstruction.Strategies;

public class ComputeShader
{
    private int _handle;

    public ComputeShader(string path)
    {
        int ComputeShader;
        
        string ComputeShaderSource = File.ReadAllText(path);

        ComputeShader = GL.CreateShader(ShaderType.ComputeShader);
        GL.ShaderSource(ComputeShader, ComputeShaderSource);
        
        GL.CompileShader(ComputeShader);
        
        GL.GetShader(ComputeShader, ShaderParameter.CompileStatus, out int successS);
        if (successS == 0)
        {
            string infoLog = GL.GetShaderInfoLog(ComputeShader);
            // TODO: Change to error style reporting
            Console.WriteLine(infoLog);
        }

        _handle = GL.CreateProgram();
        
        GL.AttachShader(_handle, ComputeShader);
        
        GL.LinkProgram(_handle);
        
        GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out int successLink);
        if (successLink == 0)
        {
            string infoLog = GL.GetProgramInfoLog(_handle);
            Console.WriteLine(infoLog);
        }
        
        GL.DetachShader(_handle, ComputeShader);
        GL.DeleteShader(ComputeShader);
    }

    public void Use()
    {
        GL.UseProgram(_handle);
    }
    
    public int GetAttribLocation(string attribName)
    {
        return GL.GetAttribLocation(_handle, attribName);
    }

    public void SetUniformInt(string name, int value)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.Uniform1(location, value);
    }

    public void SetUniformVec3(string name, ref Vector3 values)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.Uniform3(location, values);
    }

    public void SetUniformMatrix4f(string name, ref Matrix4 mat4)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.UniformMatrix4(location, true, ref mat4);
    }
    
    public void SetUniformMatrix3f(string name, ref Matrix3 mat3)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.UniformMatrix3(location, true, ref mat3);
    }

    public void SetUniformFloat(string name, float value)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.Uniform1(location, value);
    }
    
    private bool _disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            GL.DeleteProgram(_handle);

            _disposedValue = true;
        }
    }
    

    ~ComputeShader()
    {
        if (_disposedValue == false)
        {
            // TODO: Change to error warning
            Console.WriteLine("GPU Resource leak! Did you forget to call Dispose()?");
        }
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}