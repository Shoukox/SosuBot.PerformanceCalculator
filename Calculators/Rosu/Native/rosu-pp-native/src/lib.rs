#![deny(unsafe_op_in_unsafe_fn)]

mod calculation;
mod dto;
mod error;

use std::{
    ffi::{CString, c_char},
    panic::{AssertUnwindSafe, catch_unwind},
    slice,
};

use dto::{NativeErrorResponse, NativeResponse};
use error::{ErrorCode, NativeError};

const MAX_BEATMAP_LENGTH: usize = 64 * 1024 * 1024;
const MAX_REQUEST_LENGTH: usize = 64 * 1024;
const SERIALIZATION_FALLBACK: &str = r#"{"success":false,"result":null,"error":{"code":"SERIALIZATION_ERROR","message":"Failed to serialize native response"}}"#;
const PANIC_FALLBACK: &str = r#"{"success":false,"result":null,"error":{"code":"INTERNAL_PANIC","message":"Native calculation panicked"}}"#;

/// Calculate performance attributes from a beatmap and a JSON request.
///
/// The returned pointer owns a NUL-terminated UTF-8 string and must be released
/// with [`rosu_free_string`]. No Rust panic is allowed to leave this function.
///
/// # Safety
///
/// Both pointers must remain readable for their respective lengths until this
/// function returns.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn rosu_calculate(
    beatmap_ptr: *const u8,
    beatmap_len: usize,
    request_json_ptr: *const u8,
    request_json_len: usize,
) -> *mut c_char {
    let response = catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: pointer validation and conversion are isolated in
        // `calculate_from_raw`; the caller owns both buffers for this call.
        unsafe { calculate_from_raw(beatmap_ptr, beatmap_len, request_json_ptr, request_json_len) }
    }));

    match response {
        Ok(pointer) => pointer,
        Err(_) => catch_unwind(AssertUnwindSafe(|| raw_from_static(PANIC_FALLBACK)))
            .unwrap_or(std::ptr::null_mut()),
    }
}

/// Release a string returned by [`rosu_calculate`].
///
/// # Safety
///
/// `value` must be null or a pointer returned by [`rosu_calculate`] that has
/// not already been freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn rosu_free_string(value: *mut c_char) {
    if !value.is_null() {
        let _ = catch_unwind(AssertUnwindSafe(|| {
            // SAFETY: the C ABI contract requires `value` to be a pointer
            // returned by `CString::into_raw` in this library exactly once.
            unsafe {
                drop(CString::from_raw(value));
            }
        }));
    }
}

unsafe fn calculate_from_raw(
    beatmap_ptr: *const u8,
    beatmap_len: usize,
    request_json_ptr: *const u8,
    request_json_len: usize,
) -> *mut c_char {
    let result = (|| {
        // SAFETY: this function is the only layer that turns FFI pointers into
        // slices; `read_bytes` validates null, zero, and unreasonable lengths.
        let beatmap =
            unsafe { read_bytes(beatmap_ptr, beatmap_len, MAX_BEATMAP_LENGTH, "beatmap") }?;
        // SAFETY: same ownership contract as above.
        let request_json = unsafe {
            read_bytes(
                request_json_ptr,
                request_json_len,
                MAX_REQUEST_LENGTH,
                "request",
            )
        }?;

        calculation::calculate_inner(beatmap, request_json).map_err(to_error_response)
    })();

    match result {
        Ok(result) => response_to_raw(NativeResponse::success(result)),
        Err(error) => response_to_raw(NativeResponse::failure(error)),
    }
}

fn to_error_response(error: NativeError) -> NativeErrorResponse {
    NativeErrorResponse {
        code: error.code.as_str(),
        message: error.message,
    }
}

fn response_to_raw(response: NativeResponse) -> *mut c_char {
    match serde_json::to_string(&response) {
        Ok(json) => match CString::new(json) {
            Ok(value) => value.into_raw(),
            Err(_) => raw_from_static(SERIALIZATION_FALLBACK),
        },
        Err(_) => raw_from_static(SERIALIZATION_FALLBACK),
    }
}

fn raw_from_static(value: &str) -> *mut c_char {
    match CString::new(value) {
        Ok(value) => value.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

unsafe fn read_bytes<'a>(
    pointer: *const u8,
    length: usize,
    maximum_length: usize,
    argument: &str,
) -> Result<&'a [u8], NativeErrorResponse> {
    if pointer.is_null() || length == 0 {
        let message = if argument == "beatmap" {
            "Beatmap pointer must be non-null and non-empty"
        } else {
            "Request pointer must be non-null and non-empty"
        };

        return Err(NativeErrorResponse {
            code: ErrorCode::InvalidArgument.as_str(),
            message,
        });
    }

    if length > maximum_length {
        let message = if argument == "beatmap" {
            "Beatmap exceeds the maximum supported size"
        } else {
            "Request JSON exceeds the maximum supported size"
        };

        return Err(NativeErrorResponse {
            code: ErrorCode::InvalidArgument.as_str(),
            message,
        });
    }

    // SAFETY: the caller supplied a non-null pointer and an accepted length;
    // the C ABI contract requires the pointed-to memory to be readable.
    Ok(unsafe { slice::from_raw_parts(pointer, length) })
}

#[cfg(test)]
mod tests {
    use std::ffi::CStr;

    use super::{rosu_calculate, rosu_free_string};

    #[test]
    fn null_arguments_return_an_error_envelope() {
        // SAFETY: null pointers are explicitly accepted as invalid input and
        // converted into an error envelope without being dereferenced.
        let pointer = unsafe { rosu_calculate(std::ptr::null(), 0, std::ptr::null(), 0) };
        assert!(!pointer.is_null());

        // SAFETY: the pointer is owned by this test until it is released below
        // and the native function guarantees a NUL-terminated string.
        let response = unsafe { CStr::from_ptr(pointer) }.to_str().unwrap();
        assert!(response.contains("\"success\":false"));
        assert!(response.contains("\"code\":\"INVALID_ARGUMENT\""));

        // SAFETY: `pointer` is owned by this test and has not been freed yet;
        // null is explicitly accepted by the free function.
        unsafe {
            rosu_free_string(pointer);
            rosu_free_string(std::ptr::null_mut());
        }
    }

    #[test]
    fn invalid_json_is_returned_as_an_error_envelope() {
        let beatmap = include_bytes!("../../../testdata/native-fixture.osu");
        let request = b"not-json";
        // SAFETY: both byte slices remain alive for the duration of the call.
        let pointer = unsafe {
            rosu_calculate(
                beatmap.as_ptr(),
                beatmap.len(),
                request.as_ptr(),
                request.len(),
            )
        };
        assert!(!pointer.is_null());

        // SAFETY: see the ownership contract above.
        let response = unsafe { CStr::from_ptr(pointer) }.to_str().unwrap();
        assert!(response.contains("\"code\":\"INVALID_JSON\""));

        // SAFETY: `pointer` came from `rosu_calculate` and is freed once.
        unsafe { rosu_free_string(pointer) };
    }

    #[test]
    fn invalid_utf8_is_returned_as_invalid_json() {
        let beatmap = include_bytes!("../../../testdata/native-fixture.osu");
        let request = [0xff, 0xfe, 0xfd];

        // SAFETY: both byte slices remain alive for the duration of the call.
        let pointer = unsafe {
            rosu_calculate(
                beatmap.as_ptr(),
                beatmap.len(),
                request.as_ptr(),
                request.len(),
            )
        };

        // SAFETY: the pointer is valid until the matching free below.
        let response = unsafe { CStr::from_ptr(pointer) }.to_str().unwrap();
        assert!(response.contains("\"code\":\"INVALID_JSON\""));

        // SAFETY: `pointer` came from `rosu_calculate` and is freed once.
        unsafe { rosu_free_string(pointer) };
    }
}
