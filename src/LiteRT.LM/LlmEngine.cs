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

        /// <summary>Creates a new multi-turn conversation using the default config.</summary>
        public LlmConversation CreateConversation() => CreateConversation(null);

        /// <summary>Creates a new multi-turn conversation.</summary>
        /// <param name="options">Optional conversation options; null uses defaults.</param>
        public LlmConversation CreateConversation(LlmConversationOptions? options)
        {
            IntPtr config = IntPtr.Zero;
            IntPtr sessionConfig = IntPtr.Zero;
            try
            {
                if (options != null)
                {
                    config = LiteRtLmNative.litert_lm_conversation_config_create();

                    if (options.SystemInstruction != null)
                    {
                        // Pass the raw text, not a {"type":"text",...} content object: the C API
                        // keeps an unparseable string as plain string content, which every chat
                        // template accepts. A content *object* is a map in the template engine and
                        // breaks string-concat templates (e.g. Qwen's "'...' + content"); the data
                        // processors only normalize string/array content, never a bare object.
                        LiteRtLmNative.litert_lm_conversation_config_set_system_message(
                            config, options.SystemInstruction);
                    }
                    if (options.InitialMessagesJson != null)
                    {
                        LiteRtLmNative.litert_lm_conversation_config_set_messages(
                            config, options.InitialMessagesJson);
                    }
                    if (options.SamplerParams.HasValue || options.MaxOutputTokens > 0)
                    {
                        sessionConfig = LiteRtLmNative.litert_lm_session_config_create();
                        if (options.SamplerParams.HasValue)
                        {
                            var p = options.SamplerParams.Value;
                            LiteRtLmNative.litert_lm_session_config_set_sampler_params(sessionConfig, in p);
                        }
                        if (options.MaxOutputTokens > 0)
                        {
                            LiteRtLmNative.litert_lm_session_config_set_max_output_tokens(
                                sessionConfig, options.MaxOutputTokens);
                        }
                        LiteRtLmNative.litert_lm_conversation_config_set_session_config(config, sessionConfig);
                    }
                }

                var conversation = LiteRtLmNative.litert_lm_conversation_create(_engine, config);
                if (conversation == IntPtr.Zero)
                {
                    throw new InvalidOperationException("litert_lm_conversation_create returned null.");
                }
                return new LlmConversation(conversation);
            }
            finally
            {
                // The configs are copied by conversation_create; free ours only afterwards.
                if (config != IntPtr.Zero)
                {
                    LiteRtLmNative.litert_lm_conversation_config_delete(config);
                }
                if (sessionConfig != IntPtr.Zero)
                {
                    LiteRtLmNative.litert_lm_session_config_delete(sessionConfig);
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
