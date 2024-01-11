using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RGBDReconstruction.Application;

public class Shader
{
    private int _handle;

    public Shader(string vertexPath, string fragmentPath)
    {
        int VertexShader;
        int FragmentShader;
        
        string VertexShaderSource = File.ReadAllText(vertexPath);
        string FragmentShaderSource = File.ReadAllText(fragmentPath);

        VertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(VertexShader, VertexShaderSource);

        FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(FragmentShader, FragmentShaderSource);
        
        GL.CompileShader(VertexShader);
        
        GL.GetShader(VertexShader, ShaderParameter.CompileStatus, out int successS);
        if (successS == 0)
        {
            string infoLog = GL.GetShaderInfoLog(VertexShader);
            // TODO: Change to error style reporting
            Console.WriteLine(infoLog);
        }
        
        GL.CompileShader(FragmentShader);
        
        GL.GetShader(FragmentShader, ShaderParameter.CompileStatus, out int successF);
        if (successF == 0)
        {
            string infoLog = GL.GetShaderInfoLog(FragmentShader);
            // TODO: Change to error style reporting
            Console.WriteLine(infoLog);
        }

        _handle = GL.CreateProgram();
        
        GL.AttachShader(_handle, VertexShader);
        GL.AttachShader(_handle, FragmentShader);
        
        GL.LinkProgram(_handle);
        
        GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out int successLink);
        if (successLink == 0)
        {
            string infoLog = GL.GetProgramInfoLog(_handle);
            Console.WriteLine(infoLog);
        }
        
        GL.DetachShader(_handle, VertexShader);
        GL.DetachShader(_handle, FragmentShader);
        GL.DeleteShader(FragmentShader);
        GL.DeleteShader(VertexShader);
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
    
    private bool _disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            GL.DeleteProgram(_handle);

            _disposedValue = true;
        }
    }
    

    ~Shader()
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