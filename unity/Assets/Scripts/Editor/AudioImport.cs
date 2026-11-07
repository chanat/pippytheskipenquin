// Import settings for Assets/Resources/Audio. Music (Tools/make-music.py from the Higgsfield demos) is
// Vorbis q0.7: the 90-s song loops stream from disk, the short jingles decompress on load so they
// start on the frame; the synthesized effects (tools/render-audio) decompress on load too.
using UnityEditor;
using UnityEngine;

namespace Pip.EditorTools
{
    public sealed class AudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var imp = (AudioImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool music = file.StartsWith("music_"), loop = music && file.EndsWith("_loop");
            var s = imp.defaultSampleSettings;
            s.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.7f : 0.8f;
            s.preloadAudioData = !loop;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = false;
            imp.forceToMono = false;
        }
    }
}
