namespace EasySave.Localization
{
    public class LanguageFactory
    {
        public ILanguage CreateLanguage(string code)
        {
            return code.ToLower() switch
            {
                "fr" => new FrLanguage(),
                "en" => new EnLanguage(),
                _ => new EnLanguage()
            };
        }
    }
}