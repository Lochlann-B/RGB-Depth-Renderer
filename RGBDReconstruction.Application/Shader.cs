using OpenTK.Graphics.OpenGL4;

namespace RGBDReconstruction.Application;

public class Shader
{
    private int Handle;

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

        Handle = GL.CreateProgram();
        
        GL.AttachShader(Handle, VertexShader);
        GL.AttachShader(Handle, FragmentShader);
        
        GL.LinkProgram(Handle);
        
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int successLink);
        if (successLink == 0)
        {
            string infoLog = GL.GetProgramInfoLog(Handle);
            Console.WriteLine(infoLog);
        }
        
        GL.DetachShader(Handle, VertexShader);
        GL.DetachShader(Handle, FragmentShader);
        GL.DeleteShader(FragmentShader);
        GL.DeleteShader(VertexShader);
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }
    
    public int GetAttribLocation(string attribName)
    {
        return GL.GetAttribLocation(Handle, attribName);
    }
    
    private bool _disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            GL.DeleteProgram(Handle);

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