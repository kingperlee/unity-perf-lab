using PerfLab.Benchmarking;
using UnityEngine;

namespace PerfLab.Samples.DataLayout
{
    internal enum DataLayoutKind
    {
        CompactAosAllFields,
        SoaAllFields,
        CompactAosHotFields,
        WideAosHotFields,
        SoaHotFields
    }

    internal sealed class DataLayoutScenario : IBenchmarkScenario
    {
        private const int PassesPerStep = 4;
        private readonly DataLayoutKind _kind;
        private CompactItem[] _compactAos;
        private WideItem[] _wideAos;
        private float[] _px;
        private float[] _py;
        private float[] _pz;
        private float[] _vx;
        private float[] _vy;
        private float[] _vz;
        private float[][] _coldSoa;
        private int _count;
        private float _sink;

        public DataLayoutScenario(DataLayoutKind kind)
        {
            _kind = kind;
        }

        public string Name => "DataLayout-" + _kind + "-" + _count;

        public BenchmarkMeasurementMode MeasurementMode =>
            BenchmarkMeasurementMode.MeasuredStep;

        public void Setup(BenchmarkConfiguration configuration)
        {
            _count = configuration.operationsPerStep;
            switch (_kind)
            {
                case DataLayoutKind.CompactAosAllFields:
                case DataLayoutKind.CompactAosHotFields:
                    _compactAos = new CompactItem[_count];
                    InitializeCompactAos();
                    break;
                case DataLayoutKind.WideAosHotFields:
                    _wideAos = new WideItem[_count];
                    InitializeWideAos();
                    break;
                case DataLayoutKind.SoaAllFields:
                    AllocateHotSoa();
                    InitializeHotSoa();
                    break;
                case DataLayoutKind.SoaHotFields:
                    AllocateHotSoa();
                    AllocateColdSoa();
                    InitializeHotSoa();
                    break;
            }
        }

        public void ExecuteStep()
        {
            switch (_kind)
            {
                case DataLayoutKind.CompactAosAllFields:
                    UpdateCompactAos();
                    _sink = _compactAos[_count >> 1].px;
                    break;
                case DataLayoutKind.SoaAllFields:
                    UpdateSoaAllFields();
                    _sink = _px[_count >> 1];
                    break;
                case DataLayoutKind.CompactAosHotFields:
                    UpdateCompactAosHotFields();
                    _sink = _compactAos[_count >> 1].px;
                    break;
                case DataLayoutKind.WideAosHotFields:
                    UpdateWideAosHotFields();
                    _sink = _wideAos[_count >> 1].px;
                    break;
                case DataLayoutKind.SoaHotFields:
                    UpdateSoaHotFields();
                    _sink = _px[_count >> 1];
                    break;
            }
        }

        public void Cleanup()
        {
            _compactAos = null;
            _wideAos = null;
            _px = _py = _pz = null;
            _vx = _vy = _vz = null;
            _coldSoa = null;
            if (_sink == float.MinValue) Debug.Log(_sink);
        }

        private void UpdateCompactAos()
        {
            for (var pass = 0; pass < PassesPerStep; pass++)
            {
                for (var i = 0; i < _count; i++)
                {
                    _compactAos[i].vx = _compactAos[i].vx * 0.9999f + 0.0001f;
                    _compactAos[i].vy = _compactAos[i].vy * 0.9998f + 0.0002f;
                    _compactAos[i].vz = _compactAos[i].vz * 0.9997f + 0.0003f;
                    _compactAos[i].px += _compactAos[i].vx * 0.016f;
                    _compactAos[i].py += _compactAos[i].vy * 0.016f;
                    _compactAos[i].pz += _compactAos[i].vz * 0.016f;
                }
            }
        }

        private void UpdateSoaAllFields()
        {
            for (var pass = 0; pass < PassesPerStep; pass++)
            {
                for (var i = 0; i < _count; i++)
                {
                    _vx[i] = _vx[i] * 0.9999f + 0.0001f;
                    _vy[i] = _vy[i] * 0.9998f + 0.0002f;
                    _vz[i] = _vz[i] * 0.9997f + 0.0003f;
                    _px[i] += _vx[i] * 0.016f;
                    _py[i] += _vy[i] * 0.016f;
                    _pz[i] += _vz[i] * 0.016f;
                }
            }
        }

        private void UpdateWideAosHotFields()
        {
            for (var pass = 0; pass < PassesPerStep; pass++)
            {
                for (var i = 0; i < _count; i++)
                {
                    _wideAos[i].vx = _wideAos[i].vx * 0.9999f + 0.0001f;
                    _wideAos[i].px += _wideAos[i].vx * 0.016f;
                }
            }
        }

        private void UpdateCompactAosHotFields()
        {
            for (var pass = 0; pass < PassesPerStep; pass++)
            {
                for (var i = 0; i < _count; i++)
                {
                    _compactAos[i].vx = _compactAos[i].vx * 0.9999f + 0.0001f;
                    _compactAos[i].px += _compactAos[i].vx * 0.016f;
                }
            }
        }

        private void UpdateSoaHotFields()
        {
            for (var pass = 0; pass < PassesPerStep; pass++)
            {
                for (var i = 0; i < _count; i++)
                {
                    _vx[i] = _vx[i] * 0.9999f + 0.0001f;
                    _px[i] += _vx[i] * 0.016f;
                }
            }
        }

        private void AllocateHotSoa()
        {
            _px = new float[_count];
            _py = new float[_count];
            _pz = new float[_count];
            _vx = new float[_count];
            _vy = new float[_count];
            _vz = new float[_count];
        }

        private void AllocateColdSoa()
        {
            _coldSoa = new float[10][];
            for (var field = 0; field < _coldSoa.Length; field++)
                _coldSoa[field] = new float[_count];
        }

        private void InitializeCompactAos()
        {
            for (var i = 0; i < _count; i++)
            {
                float value = InitialValue(i);
                _compactAos[i] = new CompactItem
                {
                    px = value,
                    py = value + 1f,
                    pz = value + 2f,
                    vx = value * 0.1f,
                    vy = value * 0.2f,
                    vz = value * 0.3f
                };
            }
        }

        private void InitializeWideAos()
        {
            for (var i = 0; i < _count; i++)
            {
                float value = InitialValue(i);
                _wideAos[i].px = value;
                _wideAos[i].vx = value * 0.1f;
                _wideAos[i].cold0 = value + 10f;
            }
        }

        private void InitializeHotSoa()
        {
            for (var i = 0; i < _count; i++)
            {
                float value = InitialValue(i);
                _px[i] = value;
                _py[i] = value + 1f;
                _pz[i] = value + 2f;
                _vx[i] = value * 0.1f;
                _vy[i] = value * 0.2f;
                _vz[i] = value * 0.3f;
            }
        }

        private static float InitialValue(int index)
        {
            return 0.25f + (index & 1023) * 0.001f;
        }

        private struct CompactItem
        {
            public float px, py, pz;
            public float vx, vy, vz;
        }

        // 64-byte item: one typical cache line on desktop CPUs. The hot-only
        // loop needs just px/vx but pulls the cold fields into cache as well.
        private struct WideItem
        {
            public float px, py, pz;
            public float vx, vy, vz;
            public float cold0, cold1, cold2, cold3, cold4;
            public float cold5, cold6, cold7, cold8, cold9;
        }
    }
}
