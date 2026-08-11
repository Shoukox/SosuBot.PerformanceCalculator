use serde::{Deserialize, Serialize};

/// JSON request accepted by the C ABI.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct NativeRequest {
    pub mode: String,
    pub mods: u64,
    pub accuracy: Option<f64>,
    pub combo: Option<u32>,
    pub misses: u32,
    pub count300: Option<u32>,
    pub count100: Option<u32>,
    pub count50: Option<u32>,
    pub count_geki: Option<u32>,
    pub count_katu: Option<u32>,
    pub passed_objects: Option<u32>,
    pub clock_rate: Option<f64>,
    pub is_lazer: bool,
    pub large_tick_hits: Option<u32>,
    pub small_tick_hits: Option<u32>,
    pub slider_end_hits: Option<u32>,
    pub legacy_total_score: Option<u32>,
}

/// Successful result returned through the C ABI.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct NativeResult {
    pub performance_points: f64,
    pub star_rating: f64,
    pub max_combo: u32,
    pub mode: &'static str,
    pub aim_performance: Option<f64>,
    pub speed_performance: Option<f64>,
    pub accuracy_performance: Option<f64>,
    pub flashlight_performance: Option<f64>,
    pub aim_difficulty: Option<f64>,
    pub speed_difficulty: Option<f64>,
    pub speed_note_count: Option<f64>,
    pub approach_rate: Option<f64>,
    pub overall_difficulty: Option<f64>,
    pub drain_rate: Option<f64>,
    pub hit_circle_count: Option<u32>,
    pub slider_count: Option<u32>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct NativeErrorResponse {
    pub code: &'static str,
    pub message: &'static str,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct NativeResponse {
    pub success: bool,
    pub result: Option<NativeResult>,
    pub error: Option<NativeErrorResponse>,
}

impl NativeResponse {
    pub(crate) const fn success(result: NativeResult) -> Self {
        Self {
            success: true,
            result: Some(result),
            error: None,
        }
    }

    pub(crate) const fn failure(error: NativeErrorResponse) -> Self {
        Self {
            success: false,
            result: None,
            error: Some(error),
        }
    }
}
