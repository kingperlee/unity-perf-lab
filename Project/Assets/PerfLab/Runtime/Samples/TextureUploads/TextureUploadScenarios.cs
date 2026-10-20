using PerfLab.Benchmarking;
using Unity.Collections;
using UnityEngine;

namespace PerfLab.Samples.TextureUploads
{
    internal enum TextureUploadKind
    {
        CpuFillOnly,
        UploadWithoutMipmaps,
        UploadWithMipmaps,
        CpuFillAndUpload
    }

    internal sealed class TextureUploadScenario : IBenchmarkScenario
    {
        private readonly TextureUploadKind _kind;
        private NativeArray<Color32> _pixels;
        private Texture2D _texture;
        private int _edge;
        private byte _phase;

        public TextureUploadScenario(TextureUploadKind kind)
        {
            _kind = kind;
        }

        public string Name => "TextureUpload-" + _kind + "-" + _edge + "x" + _edge;

        public BenchmarkMeasurementMode MeasurementMode =>
            BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _edge = configuration.operationsPerStep;
            bool needsMipmaps = _kind == TextureUploadKind.UploadWithMipmaps;
            _texture = new Texture2D(_edge, _edge, TextureFormat.RGBA32, needsMipmaps, false);
            _pixels = _texture.GetRawTextureData<Color32>();
            FillPixels();
            _texture.Apply(needsMipmaps, false);
        }

        public void ExecuteStep()
        {
            _phase++;
            switch (_kind)
            {
                case TextureUploadKind.CpuFillOnly:
                    FillPixels();
                    break;

                case TextureUploadKind.UploadWithoutMipmaps:
                    TouchFirstPixel();
                    _texture.Apply(false, false);
                    break;

                case TextureUploadKind.UploadWithMipmaps:
                    TouchFirstPixel();
                    _texture.Apply(true, false);
                    break;

                case TextureUploadKind.CpuFillAndUpload:
                    FillPixels();
                    _texture.Apply(false, false);
                    break;
            }
        }

        public void Cleanup()
        {
            _pixels = default(NativeArray<Color32>);
            if (_texture != null) Object.Destroy(_texture);
            _texture = null;
        }

        private void FillPixels()
        {
            Color32 color = new Color32(_phase, (byte)(_phase * 3), 127, 255);
            for (var i = 0; i < _pixels.Length; i++)
                _pixels[i] = color;
        }

        private void TouchFirstPixel()
        {
            Color32 color = _pixels[0];
            color.r = _phase;
            _pixels[0] = color;
        }
    }
}
