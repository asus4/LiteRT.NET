using System;
using LiteRT.LM.Interop;

namespace LiteRT.LM
{
    /// <summary>
    /// A LiteRT-LM engine loaded from a <c>.litertlm</c> model file. Create one
    /// engine per model and reuse it to spawn <see cref="LlmSession"/> instances.
    /// </summary>
    public sealed class LlmEngine : IDisposable
    {
        private IntPtr _settings;
        private IntPtr _engine;

        private LlmEngine(IntPtr settings, IntPtr engine)
        {
            _settings = settings;
            _engine = engine;
        }

        internal IntPtr Handle => _engine;

        /// <summary>Loads an engine from a model file.</summary>
        /// <param name="modelPath">Path to a <c>.litertlm</c> model.</param>
        /// <param name="backend">Backend identifier, e.g. "cpu" or "gpu".</param>
        /// <param name="maxNumTokens">Optional max token budget (ignored when &lt;= 0).</param>
        public static LlmEngine Create(string modelPath, string backend = "cpu", int maxNumTokens = 0)
        {
            if (modelPath == null) throw new ArgumentNullException(nameof(modelPath));

            // The engine's internal LiteRT environment has no RuntimeLibraryDir, so the
            // GPU registry dlopens accelerators by bare leaf name. Pre-load them by
            // absolute path so that lookup resolves to an already-loaded image.
            if (!string.Equals(backend, "cpu", StringComparison.OrdinalIgnoreCase))
            {
                NativeAccelerators.PreloadGpu();
            }

            var settings = LiteRtLmNative.litert_lm_engine_settings_create(modelPath, backend, null, null);
            if (settings == IntPtr.Zero)
            {
                throw new InvalidOperationException("litert_lm_engine_settings_create returned null.");
            }

            if (maxNumTokens > 0)
            {
                LiteRtLmNative.litert_lm_engine_settings_set_max_num_tokens(settings, maxNumTokens);
            }

            var engine = LiteRtLmNative.litert_lm_engine_create(settings);
            if (engine == IntPtr.Zero)
            {
                LiteRtLmNative.litert_lm_engine_settings_delete(settings);
                throw new InvalidOperationException("litert_lm_engine_create returned null.");
            }

            return new LlmEngine(settings, engine);
        }

        /// <summary>Sets the global minimum log level (0=VERBOSE .. 5=FATAL, 1000=SILENT).</summary>
        public static void SetMinLogLevel(int level) => LiteRtLmNative.litert_lm_set_min_log_level(level);

        /// <summary>Creates a new inference session using the default session config.</summary>
        public LlmSession CreateSession() => CreateSession(null);

        /// <summary>Creates a new inference session.</summary>
        /// <param name="samplerParams">Optional sampler parameters; null uses defaults.</param>
        public LlmSession CreateSession(LiteRtLmSamplerParams? samplerParams)
        {
            IntPtr config = IntPtr.Zero;
            if (samplerParams.HasValue)
            {
                config = LiteRtLmNative.litert_lm_session_config_create();
                var p = samplerParams.Value;
                LiteRtLmNative.litert_lm_session_config_set_sampler_params(config, in p);
            }

            try
            {
                var session = LiteRtLmNative.litert_lm_engine_create_session(_engine, config);
                if (session == IntPtr.Zero)
                {
                    throw new InvalidOperationException("litert_lm_engine_create_session returned null.");
                }
                return new LlmSession(session);
            }
            finally
            {
                if (config != IntPtr.Zero)
                {
                    LiteRtLmNative.litert_lm_session_config_delete(config);
                }
            }
        }

        public void Dispose()
        {
            if (_engine != IntPtr.Zero)
            {
                LiteRtLmNative.litert_lm_engine_delete(_engine);
                _engine = IntPtr.Zero;
            }
            if (_settings != IntPtr.Zero)
            {
                LiteRtLmNative.litert_lm_engine_settings_delete(_settings);
                _settings = IntPtr.Zero;
            }
        }
    }
}
