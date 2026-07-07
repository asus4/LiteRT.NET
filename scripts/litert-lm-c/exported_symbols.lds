# Only the LiteRT-LM C API is exported from LiteRtLmC.framework. The statically linked
# LiteRT core must stay private: on iOS all P/Invoke goes through [DllImport("__Internal")],
# and if this framework also exported LiteRt* the core package's calls could bind to the
# copy in here (a different, ABI-incompatible LiteRT revision) instead of LiteRt.framework.
_litert_lm_*
