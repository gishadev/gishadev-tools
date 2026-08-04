namespace gishadev.tools.SavingSystem
{
    /// <summary>A string-keyed blob store — usually holding serialized JSON for one save-able system.
    /// <para>Implementations must agree on these rules, so swapping one for another can't change
    /// behaviour:</para>
    /// <list type="bullet">
    /// <item><description><see cref="Load"/> returns <c>null</c> for a key that was never saved.</description></item>
    /// <item><description>Reading never creates a key — <see cref="Exists"/> is false until you <see cref="Save"/>.</description></item>
    /// <item><description><see cref="Delete"/> and <see cref="ClearAll"/> persist, like <see cref="Save"/> does.</description></item>
    /// </list></summary>
    public interface ISaverSystem
    {
        void Save(string key, string data);

        /// <summary>The stored blob, or <c>null</c> if the key isn't set.</summary>
        string Load(string key);

        bool TryLoad(string key, out string result);
        bool Exists(string key);
        void Delete(string key);

        /// <summary>Removes everything this saver owns.</summary>
        void ClearAll();
    }
}
