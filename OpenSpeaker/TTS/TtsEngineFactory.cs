using OpenSpeaker.Models;
using OpenSpeaker.TTS.Engines;
namespace OpenSpeaker.TTS;

public class TtsEngineFactory
{
    public ITtsEngine Create(EngineConfig config)
    {
        ITtsEngine engine = config.EngineId switch
        {
            EngineIds.Sapi5 => new Sapi5Engine(),
            EngineIds.Azure => new AzureEngine(),
            EngineIds.AmazonPolly => new AmazonPollyEngine(),
            EngineIds.GoogleCloud => new GoogleCloudEngine(),
            EngineIds.ElevenLabs => new ElevenLabsEngine(),
            EngineIds.TtsMonster => new TtsMonsterEngine(),
            EngineIds.IbmWatson => new IbmWatsonEngine(),
            EngineIds.Acapela => new AcapelaEngine(),
            EngineIds.CereProc => new CereProcEngine(),
            EngineIds.UberDuck => new UberDuckEngine(),
            EngineIds.TikTok => new TikTokEngine(),
            _ => new Sapi5Engine()
        };

        if (!string.IsNullOrEmpty(config.ConfigJson) && config.ConfigJson != "{}")
            engine.Configure(config.ConfigJson);

        return engine;
    }
}
