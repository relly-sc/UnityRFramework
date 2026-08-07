using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;
using RFramework;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 使用 ExcelDataReader 将 .xlsx/.xls 工作簿转换为 ConfigPipeline 内存文档。
    /// </summary>
    public static class ExcelWorkbookReader
    {
        /// <summary>
        /// 读取工作簿中的全部非空 Sheet。
        /// </summary>
        /// <param name="filePath">Excel 文件绝对路径。</param>
        /// <returns>单 Sheet 使用文件名，多 Sheet 使用 Sheet 名的文档集合。</returns>
        public static IReadOnlyList<CsvDocument> Read(string filePath)
        {
            ValidateFile(filePath);
            RegisterCodePageProvider();

            List<CsvDocument> documents = new List<CsvDocument>();
            try
            {
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        string sheetName = reader.Name?.Trim();
                        if (string.IsNullOrEmpty(sheetName))
                        {
                            throw new RFrameworkException(
                                $"Excel workbook '{filePath}' contains an unnamed sheet.");
                        }

                        List<CsvRow> rows = ReadSheet(reader);
                        RemoveTrailingBlankRows(rows);
                        if (rows.Count == 0)
                        {
                            continue;
                        }

                        documents.Add(new CsvDocument(
                            $"{filePath}#{sheetName}", sheetName, rows));
                    }
                    while (reader.NextResult());
                }
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Failed to read Excel workbook '{filePath}'.", ex);
            }

            if (documents.Count == 0)
            {
                throw new RFrameworkException(
                    $"Excel workbook '{filePath}' contains no non-empty sheets.");
            }

            if (documents.Count == 1)
            {
                CsvDocument document = documents[0];
                documents[0] = new CsvDocument(
                    document.SourcePath,
                    Path.GetFileNameWithoutExtension(filePath),
                    document.Rows);
            }

            return documents;
        }

        private static List<CsvRow> ReadSheet(IExcelDataReader reader)
        {
            List<CsvRow> rows = new List<CsvRow>();
            int lineNumber = 0;
            while (reader.Read())
            {
                lineNumber++;
                string[] values = new string[reader.FieldCount];
                for (int columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                {
                    values[columnIndex] = FormatCellValue(reader.GetValue(columnIndex));
                }

                rows.Add(new CsvRow(lineNumber, values));
            }

            return rows;
        }

        private static string FormatCellValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            if (value is string text)
            {
                return text;
            }

            if (value is double doubleValue)
            {
                return doubleValue.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is float singleValue)
            {
                return singleValue.ToString("R", CultureInfo.InvariantCulture);
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToString("O", CultureInfo.InvariantCulture);
            }

            if (value is TimeSpan timeSpan)
            {
                return timeSpan.ToString("c", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static void RemoveTrailingBlankRows(List<CsvRow> rows)
        {
            while (rows.Count > 0 && IsBlank(rows[rows.Count - 1]))
            {
                rows.RemoveAt(rows.Count - 1);
            }
        }

        private static bool IsBlank(CsvRow row)
        {
            for (int i = 0; i < row.Values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row.Values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new RFrameworkException($"Excel file does not exist: '{filePath}'.");
            }

            string extension = Path.GetExtension(filePath);
            if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException(
                    $"Unsupported Excel extension '{extension}'. Only .xlsx and .xls are supported.");
            }
        }

        private static void RegisterCodePageProvider()
        {
            Type providerType = Type.GetType(
                "System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages");
            object instance = providerType?.GetProperty("Instance")?.GetValue(null, null);
            if (instance is EncodingProvider provider)
            {
                Encoding.RegisterProvider(provider);
            }
        }
    }
}
