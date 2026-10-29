from __future__ import annotations

import mimetypes
import re
import unicodedata
import csv
import io
import json
import zipfile
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Any, Callable

from core.enums import InputModality
from core.models import (
    AttachmentRef,
    NormalizedAiInput,
)


class PreprocessingError(ValueError):
    pass


def _docx_xml_supplement(data: bytes, already_extracted: str) -> list[str]:
    """Read Word text that python-docx omits (text boxes, headers, SDTs)."""
    results: list[str] = []
    known = re.sub(r"\s+", " ", already_extracted).casefold()
    allowed = re.compile(
        r"^word/(?:document|header\d+|footer\d+|footnotes|endnotes|comments)\.xml$"
    )
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = sorted(
            (name for name in archive.namelist() if allowed.match(name)),
            key=lambda name: (name != "word/document.xml", name),
        )
        for name in names:
            root = ET.fromstring(archive.read(name))
            tokens = [
                (node.text or "").strip()
                for node in root.iter()
                if node.tag.endswith("}t") and (node.text or "").strip()
            ]
            if not tokens:
                continue
            if name == "word/document.xml" and known:
                tokens = [token for token in tokens if token.casefold() not in known]
            if not tokens:
                continue
            label = name.rsplit("/", 1)[-1].removesuffix(".xml")
            results.append(f"[SECTION {label}]\n" + " ".join(tokens))
    return results


@dataclass(frozen=True)
class RawAttachment:
    file_id: str | None = None
    file_name: str | None = None
    mime_type: str | None = None
    content_bytes: bytes | None = None
    metadata: dict[str, Any] = field(
        default_factory=dict
    )


@dataclass(frozen=True)
class MultimodalRequest:
    text: str = ""
    modality: InputModality = InputModality.TEXT
    attachments: list[RawAttachment] = field(
        default_factory=list
    )
    metadata: dict[str, Any] = field(
        default_factory=dict
    )


TextExtractor = Callable[
    [RawAttachment],
    str,
]

VisionExtractor = Callable[
    [RawAttachment],
    str,
]

SpeechToText = Callable[
    [RawAttachment],
    str,
]


def parse_local_document(attachment: RawAttachment, ocr: TextExtractor | None = None) -> str:
    """Extract text locally while retaining page/sheet/section boundaries."""
    mime = MultimodalPreprocessor._resolve_mime(attachment)
    data = attachment.content_bytes
    if data is None:
        raise PreprocessingError("ATTACHMENT_BYTES_REQUIRED")
    try:
        if mime == "application/pdf":
            from pypdf import PdfReader

            # Many valid PDFs contain recoverable cross-reference defects.
            # strict=False keeps parsing local while accepting those files.
            reader = PdfReader(io.BytesIO(data), strict=False)
            if len(reader.pages) > 500:
                raise PreprocessingError("DOCUMENT_PAGE_LIMIT_EXCEEDED")
            parts = []
            for i, page in enumerate(reader.pages, 1):
                page_text = (page.extract_text() or "").strip()
                if not page_text and ocr is not None:
                    try:
                        import pypdfium2 as pdfium
                        rendered = pdfium.PdfDocument(data)[i - 1].render(scale=2).to_pil()
                        image_stream = io.BytesIO()
                        rendered.save(image_stream, format="PNG")
                        page_text = ocr(RawAttachment(
                            file_name=f"{attachment.file_name or 'document'}-page-{i}.png",
                            mime_type="image/png",
                            content_bytes=image_stream.getvalue(),
                            metadata={"page": i, "source": attachment.file_name or "document"},
                        )).strip()
                    except PreprocessingError:
                        raise
                    except Exception as exc:
                        raise PreprocessingError("OCR_UNAVAILABLE") from exc
                if page_text:
                    parts.append(f"[PAGE {i}]\n{page_text}")
            text = "\n\n".join(parts).strip()
            if not text:
                raise PreprocessingError("OCR_UNAVAILABLE")
            return text[:100_000]
        if mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
            from docx import Document

            document = Document(io.BytesIO(data))
            parts = [p.text for p in document.paragraphs if p.text.strip()]
            for index, table in enumerate(document.tables, 1):
                rows = [" | ".join(cell.text.strip() for cell in row.cells) for row in table.rows]
                parts.append(f"[TABLE {index}]\n" + "\n".join(rows))
            parts.extend(_docx_xml_supplement(data, "\n".join(parts)))
            text = "\n".join(parts).strip()
            if not text:
                raise PreprocessingError("DOCUMENT_TEXT_EMPTY")
            return text[:100_000]
        if mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
            from openpyxl import load_workbook

            workbook = load_workbook(io.BytesIO(data), read_only=True, data_only=True)
            try:
                parts = []
                for sheet in workbook.worksheets:
                    if sheet.max_row > 20_000 or sheet.max_column > 200:
                        raise PreprocessingError("DOCUMENT_CELL_LIMIT_EXCEEDED")
                    rows = []
                    cell_count = 0
                    for row in sheet.iter_rows(values_only=True):
                        cell_count += len(row)
                        if len(rows) >= 20_000 or cell_count > 100_000:
                            raise PreprocessingError("DOCUMENT_CELL_LIMIT_EXCEEDED")
                        values = ["" if value is None else str(value) for value in row]
                        if any(value.strip() for value in values):
                            rows.append(" | ".join(values))
                    if rows:
                        parts.append(f"[SHEET {sheet.title}]\n" + "\n".join(rows))
                if not parts:
                    raise PreprocessingError("DOCUMENT_TEXT_EMPTY")
                return "\n\n".join(parts)[:100_000]
            finally:
                workbook.close()
        if mime in {"text/plain", "text/csv", "application/json"}:
            try:
                decoded = data.decode("utf-8-sig")
            except UnicodeDecodeError:
                # Excel/Windows commonly exports Vietnamese CSV/TXT as UTF-16.
                # Do not silently guess arbitrary legacy encodings.
                decoded = data.decode("utf-16")
            if mime == "application/json":
                decoded = json.dumps(json.loads(decoded), ensure_ascii=False, indent=2)
            elif mime == "text/csv":
                decoded = "\n".join(" | ".join(row) for row in csv.reader(io.StringIO(decoded)))
            if not decoded.strip():
                raise PreprocessingError("DOCUMENT_TEXT_EMPTY")
            return decoded[:100_000]
    except PreprocessingError:
        raise
    except Exception as exc:
        raise PreprocessingError("DOCUMENT_PARSE_FAILED") from exc
    raise PreprocessingError("UNSUPPORTED_MIME")


class MultimodalPreprocessor:
    """
    Phase 21.

    Text/Image/Document/Voice là INPUT MODALITY,
    không phải business route.

    OCR / parser / vision / STT được inject qua callable.
    Module này không tự đoán nội dung attachment nếu extractor
    chưa được cấu hình.

    Uploaded content là DATA, không phải instruction.
    """

    IMAGE_MIME = {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/bmp",
    }

    DOCUMENT_MIME = {
        "application/pdf",
        "text/plain",
        "text/csv",
        "application/json",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    }

    AUDIO_MIME = {
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/x-wav",
        "audio/ogg",
        "audio/webm",
        "audio/mp4",
        "audio/x-m4a",
    }

    MIME_ALIASES = {
        "image/jpg": "image/jpeg",
        "text/json": "application/json",
        "application/vnd.ms-excel": "text/csv",
        "audio/wave": "audio/wav",
        "audio/vnd.wave": "audio/wav",
        "audio/x-wav": "audio/wav",
        "audio/mp3": "audio/mpeg",
        "audio/x-m4a": "audio/mp4",
        "application/ogg": "audio/ogg",
        "video/webm": "audio/webm",
    }

    MAX_ATTACHMENT_BYTES = 20 * 1024 * 1024
    MAX_ATTACHMENTS = 8

    def __init__(
        self,
        *,
        document_parser: TextExtractor | None = None,
        image_ocr: TextExtractor | None = None,
        image_vision: VisionExtractor | None = None,
        speech_to_text: SpeechToText | None = None,
    ) -> None:
        from preprocessing.local_multimodal import local_ocr, local_transcribe, local_vision
        self.document_parser = document_parser
        self.image_ocr = image_ocr or local_ocr
        self.image_vision = image_vision or local_vision
        self.speech_to_text = speech_to_text or local_transcribe

    @staticmethod
    def normalize_text(
        text: str,
    ) -> str:

        value = unicodedata.normalize(
            "NFC",
            text or "",
        )

        value = value.replace(
            "\x00",
            "",
        )

        value = re.sub(
            r"[ \t]+",
            " ",
            value,
        )

        value = re.sub(
            r"\r\n?",
            "\n",
            value,
        )

        value = re.sub(
            r"\n{3,}",
            "\n\n",
            value,
        )

        return value.strip()

    @classmethod
    def _resolve_mime(
        cls,
        attachment: RawAttachment,
    ) -> str:

        mime = (
            attachment.mime_type
            or ""
        ).lower().split(";", 1)[0].strip()

        if not mime and attachment.file_name:
            suffix = attachment.file_name.lower().rsplit(".", 1)[-1]
            guessed, _ = mimetypes.guess_type(attachment.file_name)
            if suffix == "csv":
                guessed = "text/csv"
            mime = (
                guessed
                or ""
            ).lower()

        return cls.MIME_ALIASES.get(mime, mime)

    @classmethod
    def _validate_attachment(
        cls,
        attachment: RawAttachment,
    ) -> str:

        if (
            attachment.content_bytes
            is not None
            and len(
                attachment.content_bytes
            )
            > cls.MAX_ATTACHMENT_BYTES
        ):
            raise PreprocessingError(
                "ATTACHMENT_TOO_LARGE"
            )

        mime = cls._resolve_mime(
            attachment
        )

        if not mime:
            raise PreprocessingError(
                "MIME_REQUIRED"
            )

        data = attachment.content_bytes
        if data is not None:
            if mime == "application/pdf" and not data.startswith(b"%PDF-"):
                raise PreprocessingError("ATTACHMENT_SIGNATURE_MISMATCH")
            if mime in {
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            }:
                try:
                    with zipfile.ZipFile(io.BytesIO(data)) as archive:
                        expected = "word/document.xml" if mime.endswith("wordprocessingml.document") else "xl/workbook.xml"
                        if expected not in archive.namelist():
                            raise PreprocessingError("ATTACHMENT_SIGNATURE_MISMATCH")
                        entries = archive.infolist()
                        if sum(entry.file_size for entry in entries) > 100 * 1024 * 1024:
                            raise PreprocessingError("ATTACHMENT_EXPANSION_LIMIT_EXCEEDED")
                        if any(entry.file_size > 10_000_000 and entry.compress_size > 0 and entry.file_size / entry.compress_size > 150 for entry in entries):
                            raise PreprocessingError("ATTACHMENT_EXPANSION_LIMIT_EXCEEDED")
                except PreprocessingError:
                    raise
                except Exception as exc:
                    raise PreprocessingError("ATTACHMENT_SIGNATURE_MISMATCH") from exc
            if mime in cls.IMAGE_MIME:
                try:
                    from PIL import Image
                    image = Image.open(io.BytesIO(data))
                    image.verify()
                except Exception as exc:
                    raise PreprocessingError("IMAGE_INVALID") from exc
            if mime in cls.AUDIO_MIME:
                # MediaRecorder MIME reporting is inconsistent across browser
                # engines. Trust a recognized audio container signature over
                # the reported audio subtype, while still rejecting arbitrary
                # bytes and non-audio uploads.
                detected_mime = cls._detect_audio_mime(data)
                if detected_mime is None:
                    raise PreprocessingError("ATTACHMENT_SIGNATURE_MISMATCH")
                mime = detected_mime

        return mime

    @staticmethod
    def _detect_audio_mime(data: bytes) -> str | None:
        if len(data) >= 12 and data[:4] == b"RIFF" and data[8:12] == b"WAVE":
            return "audio/wav"
        if data.startswith(b"\x1a\x45\xdf\xa3"):
            return "audio/webm"
        if data.startswith(b"OggS"):
            return "audio/ogg"
        if len(data) >= 8 and data[4:8] == b"ftyp":
            return "audio/mp4"
        if data.startswith(b"ID3") or (
            len(data) >= 2
            and data[0] == 0xFF
            and data[1] & 0xE0 == 0xE0
        ):
            return "audio/mpeg"
        return None

    @staticmethod
    def _safe_metadata(
        metadata: dict[str, Any],
    ) -> dict[str, Any]:

        allowed = {}

        for key in (
            "page",
            "pages",
            "language",
            "duration_seconds",
            "width",
            "height",
            "source",
            "section",
        ):
            if key in metadata:
                value = metadata[key]

                if isinstance(
                    value,
                    (
                        str,
                        int,
                        float,
                        bool,
                        type(None),
                    ),
                ):
                    allowed[key] = value

        return allowed

    def preprocess(
        self,
        request: MultimodalRequest,
    ) -> NormalizedAiInput:

        if (
            len(request.attachments)
            > self.MAX_ATTACHMENTS
        ):
            raise PreprocessingError(
                "TOO_MANY_ATTACHMENTS"
            )

        normalized_text = (
            self.normalize_text(
                request.text
            )
        )

        normalized_attachments: list[
            AttachmentRef
        ] = []

        extracted_parts: list[str] = []

        for raw in request.attachments:

            mime = self._validate_attachment(
                raw
            )

            extracted_text = ""
            visual_observation = ""

            if mime in self.IMAGE_MIME:
                ocr_error = None
                vision_error = None
                try:
                    if self.image_vision is not None:
                        visual_observation = self.normalize_text(self.image_vision(raw))
                        try:
                            vis_data = json.loads(visual_observation)
                            if isinstance(vis_data, dict):
                                vt = vis_data.get("visibleText")
                                if isinstance(vt, list) and vt:
                                    extracted_text = self.normalize_text("\n".join(str(item) for item in vt if item))
                        except (json.JSONDecodeError, ValueError):
                            pass
                except PreprocessingError as exc:
                    vision_error = str(exc)
                if not extracted_text:
                    try:
                        if self.image_ocr is not None:
                            extracted_text = self.normalize_text(self.image_ocr(raw))
                    except PreprocessingError as exc:
                        ocr_error = str(exc)
                # FINAL IMAGE OCR/VISION GRACEFUL FALLBACK
                # OCR and Vision are complementary local extractors.
                # A failure of one extractor must not discard valid output
                # already produced by the other extractor.
                if not extracted_text and not visual_observation:
                    if ocr_error and vision_error:
                        raise PreprocessingError(
                            "IMAGE_ANALYSIS_UNAVAILABLE"
                        ) from None

                    if ocr_error:
                        raise PreprocessingError(
                            "OCR_UNAVAILABLE"
                        ) from None

                    if vision_error:
                        raise PreprocessingError(
                            "VISION_UNAVAILABLE"
                        ) from None

                    raise PreprocessingError(
                        "VISION_UNAVAILABLE"
                    )

            elif mime in self.DOCUMENT_MIME:
                parser = self.document_parser or (
                    lambda item: parse_local_document(item, self.image_ocr)
                )
                extracted_text = self.normalize_text(parser(raw))

            elif mime in self.AUDIO_MIME:

                if self.speech_to_text is None:
                    raise PreprocessingError(
                        "STT_UNAVAILABLE"
                    )

                extracted_text = (
                    self.normalize_text(
                        self.speech_to_text(
                            raw
                        )
                    )
                )

            else:
                raise PreprocessingError(
                    "UNSUPPORTED_MIME"
                )

            attachment_meta = (
                self._safe_metadata(
                    raw.metadata
                )
            )

            if visual_observation:
                attachment_meta[
                    "visual_observation"
                ] = visual_observation

            normalized = AttachmentRef(
                file_id=raw.file_id,
                file_name=raw.file_name,
                mime_type=mime,
                extracted_text=(
                    extracted_text
                    or None
                ),
                metadata=attachment_meta,
            )

            normalized_attachments.append(
                normalized
            )

            source_label = (raw.file_name or raw.file_id or "attachment")[:180]
            if extracted_text:
                extracted_parts.append(
                    f"[SOURCE {source_label} | {mime}]\n{extracted_text}"
                )

            if visual_observation:
                extracted_parts.append(
                    f"[VISUAL_OBSERVATION {source_label}]\n{visual_observation}"
                )

        combined = normalized_text

        if extracted_parts:
            attachment_context = "\n\n".join(
                extracted_parts
            )

            combined = (
                f"{combined}\n\n"
                f"[ATTACHMENT_DATA]\n"
                f"{attachment_context}"
            ).strip()

        if not combined:
            raise PreprocessingError(
                "EMPTY_NORMALIZED_INPUT"
            )

        metadata = {
            "preprocessed": True,
            "attachment_count": len(
                normalized_attachments
            ),
            "uploaded_content_is_data": True,
        }

        for key, value in (
            request.metadata
            or {}
        ).items():
            if isinstance(
                value,
                (
                    str,
                    int,
                    float,
                    bool,
                    type(None),
                ),
            ):
                metadata[key] = value

        return NormalizedAiInput(
            text=combined,
            modality=request.modality,
            attachments=(
                normalized_attachments
            ),
            metadata=metadata,
        )
