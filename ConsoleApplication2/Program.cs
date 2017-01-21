using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.IO;
using System.Drawing.Imaging;

namespace OpenTKTutorial6
{
    class Camera
    {
        public Vector3 Position = Vector3.Zero;
        public Vector3 Orientation = new Vector3((float)Math.PI, 0f, 0f);
        public float MoveSpeed = 0.2f;
        public float MouseSensitivity = 0.01f;

        public Matrix4 GetViewMatrix()
        {
            Vector3 lookat = new Vector3();
            lookat.X = (float)(Math.Sin((float)Orientation.X) * Math.Cos((float)Orientation.Y));
            lookat.Y = (float)(Math.Sin((float)Orientation.Y));
            lookat.Z = (float)(Math.Cos((float)Orientation.X) * Math.Cos((float)Orientation.Y));
            return Matrix4.LookAt(Position, Position + lookat, Vector3.UnitY);
        }
        public void Move (float x, float y, float z)
        {
            Vector3 offset = new Vector3();

            Vector3 forward = new Vector3((float)Math.Sin((float)Orientation.X), 0,
                (float)Math.Cos((float)Orientation.X));
            Vector3 right = new Vector3(-forward.Z, 0, forward.X);

            offset += x * right;
            offset += y * forward;
            offset.Y += z;

            offset.NormalizeFast();
            offset = Vector3.Multiply(offset, MoveSpeed);
            Position += offset;
        }
        public void AddRotation (float x, float y)
        {
            x = x * MouseSensitivity;
            y = y * MouseSensitivity;

            Orientation.X = (Orientation.X + x) % ((float)Math.PI * 2.0f);
            Orientation.Y = Math.Max(Math.Min(Orientation.Y + y, (float)Math.PI / 2.0f - 0.1f),
                (float)-Math.PI / 2.0f + 0.1f);
        }
    }

    class Game:GameWindow
    {
        Dictionary<string, ShaderProgram> shaders = new Dictionary<string, ShaderProgram>();
        string activeShader = "default";

        int ibo_elements;

        float time = 0.0f;

        Vector3[] vertdata; //массив вершин
        Vector3[] coldata;  //массив цветов
        List<Volume> objects = new List<Volume>();
        int[] indicedata;

        Vector2[] texcoorddata;

        Camera cam = new Camera();
        Vector2 lastMousePos = new Vector2();

        public Game():base (512, 512, new OpenTK.Graphics.GraphicsMode(32,24,0,4))
        { }

        Dictionary<string, int> textures = new Dictionary<string, int>();

        void initProgram()
            //инициализация
        {
            lastMousePos = new Vector2(Mouse.X, Mouse.Y);

            //objects.Add(new Cube());
            //objects.Add(new Cube());

            GL.GenBuffers(1, out ibo_elements);
            shaders.Add("default", new ShaderProgram("vs.glsl", "fs.glsl", true));
            shaders.Add("textured", new ShaderProgram("vs_tex.glsl", "fs_tex.glsl", true));
            activeShader = "textured";
            textures.Add("opentksquare.png", loadImage("opentksquare.png"));
            textures.Add("opentksquare2.png", loadImage("opentksquare2.png"));

            TexturedCube tc = new TexturedCube();
            tc.TextureID = textures["opentksquare.png"];
            objects.Add(tc);

            TexturedCube tc2 = new TexturedCube();
            tc2.TextureID = textures["opentksquare2.png"];
            objects.Add(tc2);
            cam.Position += new Vector3(0f, 0f, 3f);

            Volume obj = ObjVolume.LoadFromFile("cow.obj");
            obj.TextureID = textures["opentksquare.png"];
            objects.Add(obj);
            Volume obj2 = ObjVolume.LoadFromFile("teapot.obj");
            obj2.Position += new Vector3(0f, 2f, 0f);
            objects.Add(obj2);
        }

        int loadImage(Bitmap image)
        {
            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);
            BitmapData data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width,
                data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            image.UnlockBits(data);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            Console.WriteLine("Image loaded");
            return texID;
        }
        int loadImage(string filename)
        {
            try
            {
                Bitmap file = new Bitmap(filename);
                return loadImage(file);
            }
            catch (FileNotFoundException e)
            {
                return -1;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            initProgram();

            Title = "Hello OpenTK! 6";
            GL.ClearColor(Color.CornflowerBlue);
            GL.PointSize(5f);
        }
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            if (Focused)
            {
                Vector2 delta = lastMousePos - 
                    new Vector2(OpenTK.Input.Mouse.GetState().X, OpenTK.Input.Mouse.GetState().Y);
                cam.AddRotation(delta.X, delta.Y);
                ResetCursor();
            }
            List<Vector3> verts = new List<Vector3>();
            List<int> inds = new List<int>();
            List<Vector3> colors = new List<Vector3>();
            List<Vector2> texcoords = new List<Vector2>();

            int vertcount = 0;

            foreach (Volume v in objects)
            {
                verts.AddRange(v.GetVerts().ToList());
                inds.AddRange(v.GetIndices(vertcount).ToList());
                colors.AddRange(v.GetColorData().ToList());
                texcoords.AddRange(v.GetTextureCoords());
                vertcount += v.VertCount;
            }
            vertdata = verts.ToArray();
            indicedata = inds.ToArray();
            coldata = colors.ToArray();
            texcoorddata = texcoords.ToArray();

            time += (float)e.Time;

            GL.BindBuffer(BufferTarget.ArrayBuffer, shaders[activeShader].GetBuffer("vPosition"));
            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, 
                (IntPtr)(vertdata.Length * Vector3.SizeInBytes),
                vertdata, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(shaders[activeShader].GetAttribute("vPosition"), 3, 
                VertexAttribPointerType.Float, false, 0, 0);

            if (shaders[activeShader].GetAttribute("vColor") != -1)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, shaders[activeShader].GetBuffer("vColor"));
                GL.BufferData<Vector3>(BufferTarget.ArrayBuffer,
                    (IntPtr)(coldata.Length * Vector3.SizeInBytes),
                    coldata, BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(shaders[activeShader].GetAttribute("vColor"), 3,
                    VertexAttribPointerType.Float, false, 0, 0);
            }
            if (shaders[activeShader].GetAttribute("texcoord") != -1)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, shaders[activeShader].GetBuffer("texcoord"));
                GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, 
                    (IntPtr)(texcoorddata.Length * Vector2.SizeInBytes), texcoorddata, 
                    BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(shaders[activeShader].GetAttribute("texcoord"), 2, 
                    VertexAttribPointerType.Float, true, 0, 0);
            }
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_elements);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(indicedata.Length * sizeof(int)),
                indicedata, BufferUsageHint.StaticDraw);

            objects[0].Position = new Vector3((float)Math.Cos(time), (float)Math.Sin(time), -3.0f);
            objects[0].Rotation = new Vector3(0.55f * time, 0.25f * time, 0);
            objects[0].Scale = new Vector3(0.1f, 0.1f, 0.1f);
            objects[1].Position = new Vector3(-1f, 0.5f + (float)Math.Cos(time), -2.0f);
            objects[1].Rotation = new Vector3(-0.25f * time, -0.35f * time, 0);
            objects[1].Scale = new Vector3(0.25f, 0.25f, 0.25f);
            foreach (Volume v in objects)
            {
                v.CalculateModelMatrix();
                v.ViewProjectionMatrix = cam.GetViewMatrix() * Matrix4.CreatePerspectiveFieldOfView(1.3f,
                    ClientSize.Width / (float)ClientSize.Height, 1.0f, 40.0f);
                v.ModelViewProjectionMatrix = v.ModelMatrix * v.ViewProjectionMatrix;
            }
            GL.UseProgram(shaders[activeShader].ProgramID);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Viewport(0, 0, Width, Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);

            shaders[activeShader].EnableVertexAttribArrays();

            int indiceat = 0;

            foreach (Volume v in objects)
            {
                GL.UniformMatrix4(shaders[activeShader].GetUniform("modelview"), false,
                    ref v.ModelViewProjectionMatrix);
                if (shaders[activeShader].GetUniform("maintexture") != -1)
                    GL.Uniform1(shaders[activeShader].GetUniform("maintexture"), 1);
                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, v.TextureID);
                GL.DrawElements(BeginMode.Triangles, v.IndiceCount, DrawElementsType.UnsignedInt,
                indiceat * sizeof(uint));
                indiceat += v.IndiceCount;
            }
            //foreach (Volume v in objects)
            //{
            //    GL.BindTexture(TextureTarget.Texture2D, v.TextureID);
            //    GL.UniformMatrix4(shaders[activeShader].GetUniform("modelview"), false, ref v.ModelViewProjectionMatrix);

            //    if (shaders[activeShader].GetAttribute("maintexture") != -1)
            //    {
            //        GL.Uniform1(shaders[activeShader].GetAttribute("maintexture"), v.TextureID);
            //    }

            //    GL.DrawElements(BeginMode.Triangles, v.IndiceCount, DrawElementsType.UnsignedInt, indiceat * sizeof(uint));
            //    indiceat += v.IndiceCount;
            //}

            GL.Flush();

            SwapBuffers();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.KeyChar == 'q') Exit();
            switch(e.KeyChar)
            {
                case 'w': cam.Move(0f, 0.1f, 0f); break;
                case 'a': cam.Move(-0.1f, 0f, 0f); break;
                case 's': cam.Move(0f, -0.1f, 0f); break;
                case 'd': cam.Move(0.1f, 0f, 0f); break;
            }
        }
        void ResetCursor()
        {
            OpenTK.Input.Mouse.SetPosition(Bounds.Left + Bounds.Width / 2, Bounds.Top + Bounds.Height / 2);
            lastMousePos = new Vector2(OpenTK.Input.Mouse.GetState().X, OpenTK.Input.Mouse.GetState().Y);
        }
        protected override void OnFocusedChanged(EventArgs e)
        {
            base.OnFocusedChanged(e);
            if (Focused)
            {
                ResetCursor();
            }
        }

    }
    class Program
    {
        static void Main(string[] args)
        {
            using (Game game = new Game())
            {
                game.Run(30, 30);
            }
        }
    }
}
