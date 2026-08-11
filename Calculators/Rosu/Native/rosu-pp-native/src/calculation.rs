use rosu_pp::{
    Beatmap, Difficulty, GameMods, Performance,
    any::{DifficultyAttributes, ScoreState},
    model::mode::GameMode,
};

use crate::{
    dto::{NativeRequest, NativeResult},
    error::{ErrorCode, NativeError},
};

pub(crate) fn calculate_inner(
    beatmap_bytes: &[u8],
    request_json: &[u8],
) -> Result<NativeResult, NativeError> {
    let request_text = std::str::from_utf8(request_json)
        .map_err(|_| NativeError::new(ErrorCode::InvalidJson, "Request JSON must be UTF-8"))?;
    let request: NativeRequest = serde_json::from_str(request_text)
        .map_err(|_| NativeError::new(ErrorCode::InvalidJson, "Request JSON is invalid"))?;

    let mode = parse_mode(&request.mode)?;
    validate_request(&request, mode)?;

    let mods_value = u32::try_from(request.mods)
        .map_err(|_| NativeError::new(ErrorCode::InvalidArgument, "Mods value is out of range"))?;
    let mods = GameMods::from(mods_value);

    let mut map = Beatmap::from_bytes(beatmap_bytes)
        .map_err(|_| NativeError::new(ErrorCode::BeatmapParseError, "Failed to parse beatmap"))?;

    if map.hit_objects.is_empty() {
        return Err(NativeError::new(
            ErrorCode::BeatmapParseError,
            "Beatmap does not contain hit objects",
        ));
    }

    if map.mode != mode {
        map = map.convert(mode, &mods).map_err(|_| {
            NativeError::new(
                ErrorCode::UnsupportedMode,
                "Beatmap mode cannot be converted",
            )
        })?;
    }

    let difficulty = build_difficulty(&request, mods.clone());
    let difficulty_attributes = difficulty.checked_calculate(&map).map_err(|_| {
        NativeError::new(
            ErrorCode::DifficultyCalculationError,
            "Difficulty calculation failed",
        )
    })?;

    validate_calculated_state(&request, &map, &difficulty_attributes)?;

    let mut performance = Performance::new(difficulty_attributes).difficulty(difficulty);

    if has_exact_score_state(&request) {
        performance = performance.state(score_state(&request));
    } else {
        if let Some(accuracy) = request.accuracy {
            performance = performance.accuracy(accuracy);
        }

        performance = performance.misses(request.misses);

        if let Some(combo) = request.combo {
            performance = performance.combo(combo);
        }
    }

    let performance_attributes = performance.checked_calculate().map_err(|_| {
        NativeError::new(
            ErrorCode::PerformanceCalculationError,
            "Performance calculation failed",
        )
    })?;

    Ok(match performance_attributes {
        rosu_pp::any::PerformanceAttributes::Osu(attributes) => NativeResult {
            performance_points: attributes.pp,
            star_rating: attributes.stars(),
            max_combo: attributes.max_combo(),
            mode: "osu",
            aim_performance: Some(attributes.pp_aim),
            speed_performance: Some(attributes.pp_speed),
            accuracy_performance: Some(attributes.pp_acc),
            flashlight_performance: Some(attributes.pp_flashlight),
            aim_difficulty: Some(attributes.difficulty.aim),
            speed_difficulty: Some(attributes.difficulty.speed),
            speed_note_count: Some(attributes.difficulty.speed_note_count),
            approach_rate: Some(attributes.difficulty.ar),
            overall_difficulty: Some(attributes.difficulty.od()),
            drain_rate: Some(attributes.difficulty.hp),
            hit_circle_count: Some(attributes.difficulty.n_circles),
            slider_count: Some(attributes.difficulty.n_sliders),
        },
        rosu_pp::any::PerformanceAttributes::Taiko(attributes) => NativeResult {
            performance_points: attributes.pp,
            star_rating: attributes.stars(),
            max_combo: attributes.max_combo(),
            mode: "taiko",
            aim_performance: None,
            speed_performance: None,
            accuracy_performance: Some(attributes.pp_acc),
            flashlight_performance: None,
            aim_difficulty: None,
            speed_difficulty: None,
            speed_note_count: None,
            approach_rate: None,
            overall_difficulty: None,
            drain_rate: None,
            hit_circle_count: None,
            slider_count: None,
        },
        rosu_pp::any::PerformanceAttributes::Catch(attributes) => NativeResult {
            performance_points: attributes.pp,
            star_rating: attributes.stars(),
            max_combo: attributes.max_combo(),
            mode: "catch",
            aim_performance: None,
            speed_performance: None,
            accuracy_performance: None,
            flashlight_performance: None,
            aim_difficulty: None,
            speed_difficulty: None,
            speed_note_count: None,
            approach_rate: None,
            overall_difficulty: None,
            drain_rate: None,
            hit_circle_count: None,
            slider_count: None,
        },
        rosu_pp::any::PerformanceAttributes::Mania(attributes) => NativeResult {
            performance_points: attributes.pp,
            star_rating: attributes.stars(),
            max_combo: attributes.max_combo(),
            mode: "mania",
            aim_performance: None,
            speed_performance: None,
            accuracy_performance: None,
            flashlight_performance: None,
            aim_difficulty: None,
            speed_difficulty: None,
            speed_note_count: None,
            approach_rate: None,
            overall_difficulty: None,
            drain_rate: None,
            hit_circle_count: None,
            slider_count: None,
        },
    })
}

fn parse_mode(value: &str) -> Result<GameMode, NativeError> {
    match value.to_ascii_lowercase().as_str() {
        "osu" | "standard" => Ok(GameMode::Osu),
        "taiko" => Ok(GameMode::Taiko),
        "catch" | "fruits" => Ok(GameMode::Catch),
        "mania" => Ok(GameMode::Mania),
        _ => Err(NativeError::new(
            ErrorCode::UnsupportedMode,
            "Requested game mode is not supported",
        )),
    }
}

fn build_difficulty(request: &NativeRequest, mods: GameMods) -> Difficulty {
    let mut difficulty = Difficulty::new().mods(mods).lazer(request.is_lazer);

    if let Some(clock_rate) = request.clock_rate {
        difficulty = difficulty.clock_rate(clock_rate);
    }

    if let Some(passed_objects) = request.passed_objects {
        difficulty = difficulty.passed_objects(passed_objects);
    }

    difficulty
}

fn has_exact_score_state(request: &NativeRequest) -> bool {
    request.count300.is_some()
        || request.count100.is_some()
        || request.count50.is_some()
        || request.count_geki.is_some()
        || request.count_katu.is_some()
        || request.large_tick_hits.is_some()
        || request.small_tick_hits.is_some()
        || request.slider_end_hits.is_some()
        || request.legacy_total_score.is_some()
}

fn validate_request(request: &NativeRequest, mode: GameMode) -> Result<(), NativeError> {
    if request.accuracy.is_none() && !has_exact_score_state(request) {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Either accuracy or a complete score state is required",
        ));
    }

    if request.accuracy.is_some() && has_exact_score_state(request) {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Accuracy and exact hit counts cannot be combined",
        ));
    }

    if let Some(accuracy) = request.accuracy
        && (!accuracy.is_finite() || !(0.0..=100.0).contains(&accuracy))
    {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Accuracy must be between 0 and 100",
        ));
    }

    if let Some(clock_rate) = request.clock_rate
        && (!clock_rate.is_finite() || !(0.01..=100.0).contains(&clock_rate))
    {
        return Err(NativeError::new(
            ErrorCode::InvalidArgument,
            "Clock rate must be between 0.01 and 100",
        ));
    }

    if request.passed_objects == Some(0) {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Passed objects must be positive",
        ));
    }

    if !has_exact_score_state(request) {
        return Ok(());
    }

    let complete = match mode {
        GameMode::Osu => {
            request.count300.is_some()
                && request.count100.is_some()
                && request.count50.is_some()
                && request.count_geki.is_none()
                && request.count_katu.is_none()
        }
        GameMode::Taiko => {
            request.count300.is_some()
                && request.count100.is_some()
                && request.count50.is_none()
                && request.count_geki.is_none()
                && request.count_katu.is_none()
        }
        GameMode::Catch => {
            request.count300.is_some()
                && request.count100.is_some()
                && request.count50.is_some()
                && request.count_geki.is_none()
                && request.count_katu.is_some()
        }
        GameMode::Mania => {
            request.count_geki.is_some()
                && request.count300.is_some()
                && request.count_katu.is_some()
                && request.count100.is_some()
                && request.count50.is_some()
        }
    };

    if !complete {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "The score state is incomplete or contains fields for another mode",
        ));
    }

    if mode != GameMode::Osu
        && (request.large_tick_hits.is_some()
            || request.small_tick_hits.is_some()
            || request.slider_end_hits.is_some()
            || request.legacy_total_score.is_some())
    {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "The supplied score fields are only valid for osu!standard",
        ));
    }

    if !request.is_lazer
        && (request.large_tick_hits.is_some()
            || request.small_tick_hits.is_some()
            || request.slider_end_hits.is_some())
    {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Lazer slider statistics require IsLazer",
        ));
    }

    if request.is_lazer && request.legacy_total_score.is_some() {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Legacy total score requires a stable score",
        ));
    }

    if mode != GameMode::Osu && request.legacy_total_score.is_some() {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Legacy total score is only valid for osu!standard",
        ));
    }

    if mode != GameMode::Mania && request.combo.is_none() {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Combo is required for this exact score state",
        ));
    }

    Ok(())
}

fn validate_calculated_state(
    request: &NativeRequest,
    map: &Beatmap,
    difficulty_attributes: &DifficultyAttributes,
) -> Result<(), NativeError> {
    let max_combo = difficulty_attributes.max_combo();

    if request.combo.is_some_and(|combo| combo > max_combo) {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Combo exceeds the calculated maximum combo",
        ));
    }

    if request
        .passed_objects
        .is_some_and(|passed| passed > map.hit_objects.len() as u32)
    {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Passed objects exceed the beatmap",
        ));
    }

    let expected_hit_results = match difficulty_attributes {
        DifficultyAttributes::Osu(attributes) => u64::from(attributes.n_objects()),
        DifficultyAttributes::Taiko(attributes) => u64::from(attributes.max_combo),
        DifficultyAttributes::Catch(attributes) => {
            u64::from(attributes.n_fruits)
                + u64::from(attributes.n_droplets)
                + u64::from(attributes.n_tiny_droplets)
        }
        DifficultyAttributes::Mania(attributes) => {
            u64::from(attributes.n_objects)
                + if request.is_lazer {
                    u64::from(attributes.n_hold_notes)
                } else {
                    0
                }
        }
    };

    if has_exact_score_state(request)
        && exact_hit_result_total(request, map.mode) != expected_hit_results
    {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Exact hit results do not match the calculated object count",
        ));
    }

    let maximum_misses = match difficulty_attributes {
        DifficultyAttributes::Catch(attributes) => {
            u64::from(attributes.n_fruits) + u64::from(attributes.n_droplets)
        }
        _ => expected_hit_results,
    };

    if u64::from(request.misses) > maximum_misses {
        return Err(NativeError::new(
            ErrorCode::InvalidScoreState,
            "Misses exceed the calculated object count",
        ));
    }

    Ok(())
}

fn exact_hit_result_total(request: &NativeRequest, mode: GameMode) -> u64 {
    let n300 = u64::from(request.count300.unwrap_or_default());
    let n100 = u64::from(request.count100.unwrap_or_default());
    let misses = u64::from(request.misses);

    match mode {
        GameMode::Osu => n300 + n100 + u64::from(request.count50.unwrap_or_default()) + misses,
        GameMode::Taiko => n300 + n100 + misses,
        GameMode::Catch => {
            n300 + n100
                + u64::from(request.count50.unwrap_or_default())
                + u64::from(request.count_katu.unwrap_or_default())
                + misses
        }
        GameMode::Mania => {
            u64::from(request.count_geki.unwrap_or_default())
                + n300
                + u64::from(request.count_katu.unwrap_or_default())
                + n100
                + u64::from(request.count50.unwrap_or_default())
                + misses
        }
    }
}

fn score_state(request: &NativeRequest) -> ScoreState {
    ScoreState {
        max_combo: request.combo.unwrap_or_default(),
        osu_large_tick_hits: request.large_tick_hits.unwrap_or_default(),
        osu_small_tick_hits: request.small_tick_hits.unwrap_or_default(),
        slider_end_hits: request.slider_end_hits.unwrap_or_default(),
        n_geki: request.count_geki.unwrap_or_default(),
        n_katu: request.count_katu.unwrap_or_default(),
        n300: request.count300.unwrap_or_default(),
        n100: request.count100.unwrap_or_default(),
        n50: request.count50.unwrap_or_default(),
        misses: request.misses,
        legacy_total_score: request.legacy_total_score,
    }
}

#[cfg(test)]
mod tests {
    use super::calculate_inner;

    const MAP: &[u8] = include_bytes!("../../../testdata/native-fixture.osu");

    fn request(extra: &str) -> Vec<u8> {
        format!(
            r#"{{"mode":"osu","mods":0,"accuracy":99.5,"combo":20,"misses":0,"count300":null,"count100":null,"count50":null,"countGeki":null,"countKatu":null,"passedObjects":null,"clockRate":null,"isLazer":true,"largeTickHits":null,"smallTickHits":null,"sliderEndHits":null,"legacyTotalScore":null{extra}}}"#
        )
        .into_bytes()
    }

    #[test]
    fn calculates_from_accuracy() {
        let result =
            calculate_inner(MAP, &request("")).expect("fixture calculation should succeed");

        assert_eq!(result.mode, "osu");
        assert!(result.performance_points.is_finite());
        assert!(result.star_rating.is_finite());
        assert_eq!(result.max_combo, 220);
    }

    #[test]
    fn rejects_accuracy_and_exact_counts_together() {
        let request = br#"{"mode":"osu","mods":0,"accuracy":99.5,"combo":20,"misses":0,"count300":20,"count100":0,"count50":0,"countGeki":null,"countKatu":null,"passedObjects":null,"clockRate":null,"isLazer":true,"largeTickHits":null,"smallTickHits":null,"sliderEndHits":null,"legacyTotalScore":null}"#;

        let error = calculate_inner(MAP, request).expect_err("mixed score state must fail");

        assert_eq!(error.code.as_str(), "INVALID_SCORE_STATE");
    }

    #[test]
    fn rejects_invalid_json() {
        let error = calculate_inner(MAP, b"not-json").expect_err("invalid JSON must fail");

        assert_eq!(error.code.as_str(), "INVALID_JSON");
    }

    #[test]
    fn rejects_damaged_beatmap() {
        let error = calculate_inner(b"not an osu beatmap", &request(""))
            .expect_err("damaged beatmap must fail");

        assert_eq!(error.code.as_str(), "BEATMAP_PARSE_ERROR");
    }

    #[test]
    fn rejects_exact_counts_that_do_not_match_objects() {
        let request = br#"{"mode":"osu","mods":0,"accuracy":null,"combo":19,"misses":0,"count300":19,"count100":0,"count50":0,"countGeki":null,"countKatu":null,"passedObjects":null,"clockRate":null,"isLazer":false,"largeTickHits":null,"smallTickHits":null,"sliderEndHits":null,"legacyTotalScore":null}"#;

        let error = calculate_inner(MAP, request).expect_err("impossible score state must fail");

        assert_eq!(error.code.as_str(), "INVALID_SCORE_STATE");
    }
}
