// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace ComSkipper
{
    // http://www.lingoes.net/en/translator/langcode.htm

    public static class Localize
    {
        #region Localization Data
        private static readonly List<localEntry> localizationList = new List<localEntry>()
        {
            new localEntry() { text = "commercial skipped", locale = "en-us", localizedText = "Commercial Skipped" },
            new localEntry() { text = "commercial skipped", locale = "en-gb", localizedText = "Advert Skipped" },
            new localEntry() { text = "commercial skipped", locale = "es-es", localizedText = "Comercial Salteado" },
            new localEntry() { text = "commercial skipped", locale = "es-mx", localizedText = "Comercial Salteado" },
            new localEntry() { text = "commercial skipped", locale = "es-pr", localizedText = "Comercial Salteado" },
            new localEntry() { text = "commercial skipped", locale = "fr-ca", localizedText = "Commercial Ignoré" },
            new localEntry() { text = "commercial skipped", locale = "fr-fr", localizedText = "Commercial Sauté" },
            new localEntry() { text = "commercial skipped", locale = "sv-se", localizedText = "Kommersiell överhoppad" },
            new localEntry() { text = "commercial skipped", locale = "ja-jp", localizedText = "コマーシャルスキップ" },
            new localEntry() { text = "commercial skipped", locale = "zh-cn", localizedText = "商业跳过" },
            new localEntry() { text = "commercial skipped", locale = "it-it", localizedText = "Commerciale saltato" },
            new localEntry() { text = "commercial skipped", locale = "en-IE", localizedText = "Scipeáil Tráchtála" },
            new localEntry() { text = "commercial skipped", locale = "de-de", localizedText = "Werbung übersprungen" },
            new localEntry() { text = "commercial skipped", locale = "de",    localizedText = "Werbung übersprungen" },
            new localEntry() { text = "commercial skipped", locale = "sv-se", localizedText = "Kommersiell överhoppad" }
        };
        #endregion Localization Data

        /// <summary>
        /// Localize the given string to the given locale.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="locale"></param>
        /// <returns>Localized string.  If the locale or the string are unknown, return the given string.</returns>
        public static string localize(string str, string locale)
        {
            localEntry found = localizationList.Find(x => x.text == str.ToLower() && x.locale == locale.ToLower());
            if (found == null)
                return str;

            return found.localizedText;
        }
    }

    public class localEntry
    {
        public string text { get; set; } // all lower case

        public string locale { get; set; }  // all lower case

        public string localizedText { get; set; }
    }
}
