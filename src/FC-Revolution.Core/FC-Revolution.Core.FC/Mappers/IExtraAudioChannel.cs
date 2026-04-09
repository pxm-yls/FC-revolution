namespace FCRevolution.Core.Mappers;

public interface IExtraAudioChannel
{
    float ExtraAudioSample { get; }
    void ClockExtraAudio();
    void ResetExtraAudio();
}
