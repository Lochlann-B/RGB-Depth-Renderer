using Emgu.CV.Ocl;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RGBDReconstruction.Strategies;

namespace RGBDReconstruction.Application;

public class BVHReconstruction : IReconstructionApplication
{
    private int[] _indexArray;

    private Vector3 _lightPos;

    private int _vertexBufferObject;
    private int _vertexArrayObject;
    private int _elementBufferObject;

    private int _lightVertexArrayObject;

    private int _width;
    private int _height;

    private Camera _camera;

    private Shader _shader;
    private Shader _lightingShader;

    private Matrix4 _model;
    private Matrix4 _view;
    private Matrix4 _projection;

    private Matrix4 _lightModel;

    private Vector2 _prevMousePos;
    private float _sensitivity;
    private bool firstMove = true;

    private Texture _diffuseMap;
    private Texture _specularMap;
    
    public void Init(int windowWidth, int windowHeight)
    {
        _width = windowWidth;
        _height = windowHeight;
        
        GL.Enable(EnableCap.DepthTest);

        GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        _camera = new Camera();
        _sensitivity = 0.1f;
        
        _vertexBufferObject = GL.GenBuffer();
        
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);


        var multiViewReconstructor = new MultiViewVoxelGridReconstruction(0, 500);
        var mesh = multiViewReconstructor.GetFrameGeometry(1);
        var contiguousMeshData = mesh.GetContiguousMeshData();
        
        GL.BufferData(BufferTarget.ArrayBuffer, contiguousMeshData.Length * sizeof(float), contiguousMeshData, BufferUsageHint.StaticDraw);
        
        // VAO - Vertex Array Object - specifies how the vertex attribute information is stored, formatted, and which buffers the data comes from
        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);
        
        // Element buffer needs to be bound *after* the VAO has been bound
        
        _lightingShader = new Shader("./shaders/shader.vert", "./shaders/lighting.frag");
        _lightingShader.Use();
        
        // The 'index' param below refers to the location in the shader program we are putting data into.
        // That is, the layout (location = 0) in shader.vert
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);

        var normalLoc = _lightingShader.GetAttribLocation("aNormal");
        GL.EnableVertexAttribArray(normalLoc);
        GL.VertexAttribPointer(normalLoc, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));

        var texLoc = _lightingShader.GetAttribLocation("aTexCoords");
        GL.EnableVertexAttribArray(texLoc);
        GL.VertexAttribPointer(texLoc, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
        
        _elementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
        _indexArray = mesh.MeshLayout.IndexArray;
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.MeshLayout.IndexArray.Length * sizeof(uint), mesh.MeshLayout.IndexArray, BufferUsageHint.StaticDraw);
        
        // The VBO that the VAO takes its data from is determined by the VBO currently bound to the ArrayBuffer (i.e. that on line 43)

        var objectColor = new Vector3(0.5f, 0.5f, 0.0f);
        var lightColor = new Vector3(1.0f, 1.0f, 1.0f);
        _lightingShader.SetUniformVec3("objectColor", ref objectColor);
        _lightingShader.SetUniformVec3("lightColor", ref lightColor);

        _model = Matrix4.Identity;
        _model[0, 0] = 1f;
        _model[1, 1] = 1f;
        _model[2, 2] = -1f;
        _view = Matrix4.CreateTranslation(0.0f, 0.0f, -0.0f);
        _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), windowWidth / (float)windowHeight, 0.1f, 100.0f);
        _lightingShader.SetUniformMatrix4f("model", ref _model);
        _lightingShader.SetUniformMatrix4f("view", ref _view);
        _lightingShader.SetUniformMatrix4f("projection", ref _projection);

        _lightPos = new Vector3(-0.0f, -0.0f, -1.0f);



        //_diffuseMap = new Texture("./resources/container2.png");
        _diffuseMap = new Texture("C:\\Users\\Locky\\Desktop\\renders\\chain_collision\\rgb\\frame_0001_cam_001.png");
        _diffuseMap.Use(TextureUnit.Texture0);

        //_specularMap = new Texture("./resources/container2_specular.png");
        //_specularMap.Use(TextureUnit.Texture1);
    }

    public void RenderFrame(FrameEventArgs args)
    {
        _view = _camera.LookAt;
        _view[3, 0] *= -1;
        _view[3, 1] *= -1;
        _view[3, 2] *= -1;
        _projection = _camera.CameraProjectionMatrix;

        
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        GL.BindVertexArray(_vertexArrayObject);
        _diffuseMap.Use(TextureUnit.Texture0);

        
        _lightingShader.Use();
        _lightingShader.SetUniformMatrix4f("model", ref _model);
        _lightingShader.SetUniformMatrix4f("view", ref _view);
        _lightingShader.SetUniformMatrix4f("projection", ref _projection);

        var vLightPos = new Vector4(_lightPos, 0.0f);
        vLightPos *= _view;
        var v3LightPos = vLightPos.Xyz;

        var normalMat = new Matrix3(Matrix4.Transpose(Matrix4.Invert(Matrix4.Mult(_model, _view))));
        _lightingShader.SetUniformMatrix3f("normal", ref normalMat);
        
        var objAmbient = new Vector3(1.0f, 0.5f, 0.31f);
        var objDiffuse = new Vector3(1.0f, 0.5f, 0.31f);
        var objSpecular = new Vector3(0.5f, 0.5f, 0.5f);
        var objShininess = 32;

        var lightAmbient = new Vector3(0.2f, 0.2f, 0.2f);
        var lightDiffuse = new Vector3(0.5f, 0.5f, 0.5f);
        var lightSpecular = new Vector3(1.0f, 1.0f, 1.0f);
        _lightingShader.SetUniformVec3("material.specular", ref objSpecular);
        _lightingShader.SetUniformFloat("material.shininess", objShininess);
        
        _lightingShader.SetUniformVec3("light.ambient", ref lightAmbient);
        _lightingShader.SetUniformVec3("light.diffuse", ref lightDiffuse);
        _lightingShader.SetUniformVec3("light.specular", ref lightSpecular);
        _lightingShader.SetUniformVec3("light.direction", ref v3LightPos);
        
        
        _lightingShader.SetUniformInt("material.diffuse", 0);
        _lightingShader.SetUniformInt("material.specular", 1);
        
        GL.DrawElements(PrimitiveType.Triangles, _indexArray.Length, DrawElementsType.UnsignedInt, 0);
    }

    public void Resize(ResizeEventArgs e)
    {
        _width = e.Width;
        _height = e.Height;

        _camera.AspectRatio = _width / (float)_height;
        
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    public void UpdateFrame(bool IsFocused, Vector2 MousePosition, KeyboardState keyboardState, FrameEventArgs args)
    {
        if (firstMove)
        {
            _prevMousePos = new Vector2(MousePosition.X, MousePosition.Y);
            firstMove = false;
        }
        if (IsFocused)
        {
            float deltaX = MousePosition.X - _prevMousePos.X;
            float deltaY = MousePosition.Y - _prevMousePos.Y;
            _prevMousePos.X = MousePosition.X;
            _prevMousePos.Y = MousePosition.Y;
            
            _camera.HandleInput(keyboardState, new Vector2(deltaX, deltaY), _sensitivity, args.Time);
        }
    }
    

    public void ReconstructScene(int sceneNo, int voxelGridSize)
    {
        
    }

    public void MouseWheel(MouseWheelEventArgs e)
    {
        _camera.Fov -= e.OffsetY*0.02f;
    }
}