namespace LiteRT.Unity
{
    public enum AspectMode
    {
        /// <summary>Stretches to the model input size, ignoring aspect ratio.</summary>
        None = 0,
        /// <summary>Keeps aspect ratio, letterboxing with black pixels.</summary>
        Fit = 1,
        /// <summary>Keeps aspect ratio, cropping the overflow.</summary>
        Fill = 2,
    }
}
