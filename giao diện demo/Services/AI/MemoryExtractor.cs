using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI
{
    /// <summary>
    /// Phase 04 Memory Extractor.
    ///
    /// Extractor chỉ tạo candidate.
    /// Nó KHÔNG:
    /// - truy cập database
    /// - quyết định authorization
    /// - tự lưu memory
    /// - đọc memory của user khác
    ///
    /// Phase sau có thể thay rule-based extraction bằng AVA-HRMS/LLM,
    /// nhưng output contract vẫn giữ nguyên.
    /// </summary>
    public sealed class MemoryExtractor : IMemoryExtractor
    {
        private static readonly HashSet<string> BlockedTerms =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "password",
                "mật khẩu",
                "api key",
                "apikey",
                "access token",
                "refresh token",
                "secret",
                "otp",
                "mã otp",
                "private key",
                "connection string",
                "cookie",
                "authorization bearer",
                "bearer token"
            };


        public MemoryExtractionResult Extract(
            string userMessage
        )
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return Reject("empty_message");
            }

            var text = userMessage.Trim();

            if (text.Length > 4000)
            {
                return Reject("message_too_long");
            }

            if (ContainsBlockedData(text))
            {
                return Reject("sensitive_data");
            }


            // -------------------------------------------------
            // RESPONSE STYLE
            // -------------------------------------------------

            if (
                ContainsAny(
                    text,
                    "trả lời ngắn",
                    "trả lời ngắn gọn",
                    "nói ngắn gọn",
                    "ưu tiên câu trả lời ngắn",
                    "đừng trả lời dài"
                )
            )
            {
                return Accept(
                    memoryType: "response_style",
                    memoryKey: "answer_length",
                    content: "Ưu tiên câu trả lời ngắn gọn.",
                    confidence: 0.98
                );
            }


            if (
                ContainsAny(
                    text,
                    "trả lời chi tiết",
                    "giải thích chi tiết",
                    "nói kỹ",
                    "giải thích kỹ"
                )
            )
            {
                return Accept(
                    memoryType: "response_style",
                    memoryKey: "answer_length",
                    content: "Ưu tiên câu trả lời chi tiết.",
                    confidence: 0.95
                );
            }


            // -------------------------------------------------
            // LANGUAGE
            // -------------------------------------------------

            if (
                ContainsAny(
                    text,
                    "trả lời bằng tiếng việt",
                    "nói tiếng việt",
                    "dùng tiếng việt"
                )
            )
            {
                return Accept(
                    memoryType: "preference",
                    memoryKey: "preferred_language",
                    content: "Ưu tiên sử dụng tiếng Việt.",
                    confidence: 0.99
                );
            }


            if (
                ContainsAny(
                    text,
                    "trả lời bằng tiếng anh",
                    "nói tiếng anh",
                    "dùng tiếng anh"
                )
            )
            {
                return Accept(
                    memoryType: "preference",
                    memoryKey: "preferred_language",
                    content: "Ưu tiên sử dụng tiếng Anh.",
                    confidence: 0.99
                );
            }


            // -------------------------------------------------
            // WORK CONTEXT
            // -------------------------------------------------

            var workMatch = Regex.Match(
                text,
                @"(?i)\b(?:tôi|mình)\s+(?:đang\s+)?(?:phụ trách|làm về|làm)\s+(.+?)(?:[.!?]|$)"
            );

            if (workMatch.Success)
            {
                var work = NormalizeValue(
                    workMatch.Groups[1].Value
                );

                if (
                    work.Length >= 3 &&
                    work.Length <= 300
                )
                {
                    return Accept(
                        memoryType: "work_context",
                        memoryKey: "main_work_area",
                        content: $"Người dùng phụ trách/làm về {work}.",
                        confidence: 0.85
                    );
                }
            }


            // -------------------------------------------------
            // EXPLICIT PREFERENCE
            // -------------------------------------------------

            var preferenceMatch = Regex.Match(
                text,
                @"(?i)\b(?:tôi|mình)\s+(?:thích|ưu tiên)\s+(.+?)(?:[.!?]|$)"
            );

            if (preferenceMatch.Success)
            {
                var preference = NormalizeValue(
                    preferenceMatch.Groups[1].Value
                );

                if (
                    preference.Length >= 3 &&
                    preference.Length <= 300
                )
                {
                    return Accept(
                        memoryType: "preference",
                        memoryKey: "general_preference",
                        content: $"Người dùng ưu tiên {preference}.",
                        confidence: 0.80
                    );
                }
            }


            return Reject(
                "no_stable_memory_detected"
            );
        }


        private static bool ContainsBlockedData(
            string text
        )
        {
            foreach (var blocked in BlockedTerms)
            {
                if (
                    text.Contains(
                        blocked,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }


        private static bool ContainsAny(
            string text,
            params string[] values
        )
        {
            foreach (var value in values)
            {
                if (
                    text.Contains(
                        value,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }


        private static string NormalizeValue(
            string value
        )
        {
            var normalized = Regex.Replace(
                value.Trim(),
                @"\s+",
                " "
            );

            return normalized.Trim(
                ' ',
                '.',
                ',',
                ';',
                ':'
            );
        }


        private static MemoryExtractionResult Accept(
            string memoryType,
            string memoryKey,
            string content,
            double confidence
        )
        {
            return new MemoryExtractionResult
            {
                ShouldStore = true,

                Candidate = new MemoryCandidate
                {
                    MemoryType = memoryType,
                    MemoryKey = memoryKey,
                    Content = content,
                    Confidence = confidence,
                    Source = "conversation"
                }
            };
        }


        private static MemoryExtractionResult Reject(
            string reason
        )
        {
            return new MemoryExtractionResult
            {
                ShouldStore = false,
                RejectionReason = reason,
                Candidate = null
            };
        }
    }
}
