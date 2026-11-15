using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DynamicIsland.Helpers
{
    public static class MiniCalculator
    {
        public static string Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return string.Empty;

            expression = expression.Trim().Replace(',', '.');

            // Конвертация валют
            // Пример: "100 usd в rub", "50 eur в rub", "1000 rub в usd"
            var matchUsd = Regex.Match(expression, @"^(\d+(\.\d+)?)\s*(usd|\$)\s*(в|to|in)\s*(rub|руб)$", RegexOptions.IgnoreCase);
            if (matchUsd.Success && double.TryParse(matchUsd.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double valUsd))
            {
                return $"{valUsd * 96.5:N1} ₽";
            }

            var matchEur = Regex.Match(expression, @"^(\d+(\.\d+)?)\s*(eur|€)\s*(в|to|in)\s*(rub|руб)$", RegexOptions.IgnoreCase);
            if (matchEur.Success && double.TryParse(matchEur.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double valEur))
            {
                return $"{valEur * 105.0:N1} ₽";
            }

            var matchRub = Regex.Match(expression, @"^(\d+(\.\d+)?)\s*(rub|руб|₽)\s*(в|to|in)\s*(usd|\$)$", RegexOptions.IgnoreCase);
            if (matchRub.Success && double.TryParse(matchRub.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double valRub))
            {
                return $"${valRub / 96.5:N2}";
            }

            // Математические операции через DataTable Compute
            try
            {
                // Очистка от запрещенных символов
                string sanitized = Regex.Replace(expression, @"[^0-9\+\-\*\/\.\(\)\%\^]", "");
                if (string.IsNullOrWhiteSpace(sanitized)) return string.Empty;

                using var table = new DataTable();
                var result = table.Compute(sanitized, null);
                if (result != null && double.TryParse(result.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                {
                    return num.ToString("0.######", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            return string.Empty;
        }
    }
}
