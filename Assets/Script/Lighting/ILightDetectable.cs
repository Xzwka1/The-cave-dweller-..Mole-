namespace CaveDweller.Lighting
{
    public interface ILightDetectable
    {
        void OnIlluminated(float intensity);
        void OnDarkened();
    }
}
