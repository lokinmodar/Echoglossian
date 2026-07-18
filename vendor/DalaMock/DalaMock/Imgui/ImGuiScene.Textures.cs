using Texture = Veldrid.Texture;

namespace DalaMock.Core.Imgui;

public partial class ImGuiScene
{
    /// <summary>
    /// Loads a image from a path and creates a dalamud compatible texture wrap.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>A dalamud compatible texture wrap.</returns>
    public IDalamudTextureWrap LoadImage(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        var image = Stbi.LoadFromMemory(ms, 4);
        var texture = this.GraphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)image.Width,
                (uint)image.Height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled));
        var handle = this.GetOrCreateImGuiBinding(this.GraphicsDevice.ResourceFactory, texture);
        this.GraphicsDevice.UpdateTexture(texture, image.Data, 0, 0, 0, (uint)image.Width, (uint)image.Height, 1, 0, 0);
        var veldridTextureWrap = new RawTextureMap(handle, image.Width, image.Height);
        return veldridTextureWrap;
    }

    /// <summary>
    /// Loads a image from an array of bytes and creates a dalamud compatible texture wrap.
    /// </summary>
    /// <param name="imageData">The bytes to load.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>A dalamud compatible texture wrap.</returns>
    public unsafe IDalamudTextureWrap LoadImageRaw(byte[] imageData, uint width, uint height)
    {
        IDalamudTextureWrap ret;

        fixed (void* pixelData = imageData)
        {
            var texture = this.GraphicsDevice.ResourceFactory.CreateTexture(
                TextureDescription.Texture2D(
                    width,
                    height,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled));
            var handle = this.GetOrCreateImGuiBinding(this.GraphicsDevice.ResourceFactory, texture);
            this.GraphicsDevice.UpdateTexture(texture, imageData, 0, 0, 0, width, height, 1, 0, 0);
            var veldridTextureWrap = new RawTextureMap(handle, (int)width, (int)height);
            return veldridTextureWrap;
        }

        return ret;
    }

    public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, Texture texture)
    {
        if (!this.autoViewsByTexture.TryGetValue(texture, out var textureView))
        {
            textureView = factory.CreateTextureView(texture);
            this.autoViewsByTexture.Add(texture, textureView);
            this.ownedResources.Add(textureView);
        }

        return this.GetOrCreateImGuiBinding(factory, textureView);
    }

    public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, TextureView textureView)
    {
        if (!this.setsByView.TryGetValue(textureView, out var rsi))
        {
            var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(this.textureLayout, textureView));
            rsi = new ResourceSetInfo(this.GetNextImGuiBindingId(), resourceSet);

            this.setsByView.Add(textureView, rsi);
            this.viewsById.Add(rsi.ImGuiBinding, rsi);
            this.ownedResources.Add(resourceSet);
        }

        return rsi.ImGuiBinding;
    }

    public void CleanupImGuiBinding(IntPtr binding)
    {
        if (this.viewsById.Remove(binding, out var rsi))
        {
            // try to get the texture view from the resource set
            var textureView = this.setsByView.FirstOrDefault(x => x.Value.ImGuiBinding == rsi.ImGuiBinding).Key;
            if (textureView is not null)
            {
                var texture = this.autoViewsByTexture.FirstOrDefault(x => x.Value == textureView).Key;
                if (texture is not null)
                {
                    this.autoViewsByTexture.Remove(texture);
                    texture.Dispose();
                }

                this.ownedResources.Remove(textureView);
                textureView.Dispose();

                this.setsByView.Remove(textureView);
            }

            this.ownedResources.Remove(rsi.ResourceSet);
            rsi.ResourceSet.Dispose();
        }
    }

    private IntPtr GetNextImGuiBindingId()
    {
        var newId = this.lastAssignedId++;
        return newId;
    }
}
