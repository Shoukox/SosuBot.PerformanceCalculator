#[allow(dead_code)]
#[derive(Copy, Clone, Debug)]
pub(crate) enum ErrorCode {
    InvalidArgument,
    InvalidJson,
    BeatmapParseError,
    InvalidScoreState,
    UnsupportedMode,
    DifficultyCalculationError,
    PerformanceCalculationError,
    SerializationError,
    InternalPanic,
}

impl ErrorCode {
    pub(crate) const fn as_str(self) -> &'static str {
        match self {
            Self::InvalidArgument => "INVALID_ARGUMENT",
            Self::InvalidJson => "INVALID_JSON",
            Self::BeatmapParseError => "BEATMAP_PARSE_ERROR",
            Self::InvalidScoreState => "INVALID_SCORE_STATE",
            Self::UnsupportedMode => "UNSUPPORTED_MODE",
            Self::DifficultyCalculationError => "DIFFICULTY_CALCULATION_ERROR",
            Self::PerformanceCalculationError => "PERFORMANCE_CALCULATION_ERROR",
            Self::SerializationError => "SERIALIZATION_ERROR",
            Self::InternalPanic => "INTERNAL_PANIC",
        }
    }
}

#[derive(Copy, Clone, Debug)]
pub(crate) struct NativeError {
    pub(crate) code: ErrorCode,
    pub(crate) message: &'static str,
}

impl NativeError {
    pub(crate) const fn new(code: ErrorCode, message: &'static str) -> Self {
        Self { code, message }
    }
}
