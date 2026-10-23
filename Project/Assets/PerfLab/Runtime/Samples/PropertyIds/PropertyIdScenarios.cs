using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.PropertyIds
{
    internal enum PropertyIdKind
    {
        MaterialSetColorString,
        MaterialSetColorCachedId,
        ShaderPropertyToId,
        CachedShaderPropertyId,
        AnimatorStringToHash,
        CachedAnimatorHash
    }

    internal sealed class PropertyIdScenario : IBenchmarkScenario
    {
        private const string ShaderPropertyName = "_Color";
        private const string AnimatorParameterName = "MovementSpeed";

        private readonly PropertyIdKind _kind;
        private Material _material;
        private int _operations;
        private int _cachedShaderPropertyId;
        private int _cachedAnimatorHash;
        private int _integerSink;
        private Color _colorSink;
        private bool _toggle;

        public PropertyIdScenario(PropertyIdKind kind)
        {
            _kind = kind;
        }

        public string Name => "PropertyIds-" + _kind;

        public BenchmarkMeasurementMode MeasurementMode => BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _operations = configuration.operationsPerStep;
            _cachedShaderPropertyId = Shader.PropertyToID(ShaderPropertyName);
            _cachedAnimatorHash = Animator.StringToHash(AnimatorParameterName);

            Shader shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new System.InvalidOperationException("A built-in color shader is required.");
            _material = new Material(shader);
        }

        public void ExecuteStep()
        {
            _toggle = !_toggle;
            Color color = _toggle ? Color.white : Color.gray;
            int value = 0;

            switch (_kind)
            {
                case PropertyIdKind.MaterialSetColorString:
                    for (int i = 0; i < _operations; i++)
                        _material.SetColor(ShaderPropertyName, color);
                    _colorSink = _material.GetColor(_cachedShaderPropertyId);
                    break;

                case PropertyIdKind.MaterialSetColorCachedId:
                    for (int i = 0; i < _operations; i++)
                        _material.SetColor(_cachedShaderPropertyId, color);
                    _colorSink = _material.GetColor(_cachedShaderPropertyId);
                    break;

                case PropertyIdKind.ShaderPropertyToId:
                    for (int i = 0; i < _operations; i++)
                        value = unchecked(value * 31 + Shader.PropertyToID(ShaderPropertyName));
                    _integerSink ^= value;
                    break;

                case PropertyIdKind.CachedShaderPropertyId:
                    for (int i = 0; i < _operations; i++)
                        value = unchecked(value * 31 + _cachedShaderPropertyId);
                    _integerSink ^= value;
                    break;

                case PropertyIdKind.AnimatorStringToHash:
                    for (int i = 0; i < _operations; i++)
                        value = unchecked(value * 31 + Animator.StringToHash(AnimatorParameterName));
                    _integerSink ^= value;
                    break;

                case PropertyIdKind.CachedAnimatorHash:
                    for (int i = 0; i < _operations; i++)
                        value = unchecked(value * 31 + _cachedAnimatorHash);
                    _integerSink ^= value;
                    break;
            }
        }

        public void Cleanup()
        {
            if (_material != null)
            {
                Object.Destroy(_material);
                _material = null;
            }
            if (_integerSink == int.MinValue || _colorSink.r == float.MinValue)
                Debug.Log(_integerSink);
        }
    }
}
