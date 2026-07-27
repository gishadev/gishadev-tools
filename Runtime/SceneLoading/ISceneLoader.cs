using Cysharp.Threading.Tasks;

namespace gishadev.tools.SceneLoading
{
    public interface ISceneLoader
    {
        UniTask LoadScene(string sceneToLoad, bool fade = true);
    }
}
