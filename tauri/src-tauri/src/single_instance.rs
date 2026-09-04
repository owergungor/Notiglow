//! Native single-instance protection on Windows using a named system mutex.
//! Ensures only one instance of Curry runs at any time, preventing duplicate listeners,
//! tray icons, and background worker threads.

#[cfg(target_os = "windows")]
pub mod platform {
    use std::ffi::c_void;

    type HANDLE = *mut c_void;
    type HWND = *mut c_void;
    type BOOL = i32;
    type DWORD = u32;
    type LPCWSTR = *const u16;

    const ERROR_ALREADY_EXISTS: DWORD = 183;
    const ERROR_ACCESS_DENIED: DWORD = 5;
    const SW_RESTORE: i32 = 9;

    #[link(name = "kernel32")]
    extern "system" {
        fn CreateMutexW(lpMutexAttributes: *mut c_void, bInitialOwner: BOOL, lpName: LPCWSTR) -> HANDLE;
        fn GetLastError() -> DWORD;
        fn SetLastError(dwErrCode: DWORD);
        fn CloseHandle(hObject: HANDLE) -> BOOL;
    }

    #[link(name = "user32")]
    extern "system" {
        fn FindWindowW(lpClassName: LPCWSTR, lpWindowName: LPCWSTR) -> HWND;
        fn ShowWindow(hWnd: HWND, nCmdShow: i32) -> BOOL;
        fn SetForegroundWindow(hWnd: HWND) -> BOOL;
    }

    pub struct SingleInstanceGuard {
        handles: Vec<HANDLE>,
    }

    unsafe impl Send for SingleInstanceGuard {}
    unsafe impl Sync for SingleInstanceGuard {}

    impl Drop for SingleInstanceGuard {
        fn drop(&mut self) {
            for handle in self.handles.drain(..) {
                if !handle.is_null() {
                    unsafe {
                        CloseHandle(handle);
                    }
                }
            }
        }
    }

    fn restore_primary_window() {
        unsafe {
            let wide_curry: Vec<u16> = "Curry".encode_utf16().chain(std::iter::once(0)).collect();
            let mut hwnd = FindWindowW(std::ptr::null(), wide_curry.as_ptr());
            if hwnd.is_null() {
                // [LEGACY / BACKWARDS COMPATIBILITY] Window title fallback to locate running legacy process
                let wide_notiglow: Vec<u16> = "NotiGlow".encode_utf16().chain(std::iter::once(0)).collect();
                hwnd = FindWindowW(std::ptr::null(), wide_notiglow.as_ptr());
            }
            if !hwnd.is_null() {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
        }
    }

    fn try_acquire_named_mutex(name: &str) -> Option<HANDLE> {
        let wide_name: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
        unsafe {
            SetLastError(0);
            let handle = CreateMutexW(std::ptr::null_mut(), 1, wide_name.as_ptr());
            if handle.is_null() {
                // If Global namespace failed due to permissions (e.g., standard non-admin account),
                // fall back immediately to local session namespace
                if name.starts_with("Global\\") {
                    let local_name = &name["Global\\".len()..];
                    return try_acquire_named_mutex(local_name);
                }

                let err = GetLastError();
                if err == ERROR_ACCESS_DENIED || err == ERROR_ALREADY_EXISTS {
                    return None;
                }
                return None;
            }

            if GetLastError() == ERROR_ALREADY_EXISTS {
                CloseHandle(handle);
                return None;
            }

            Some(handle)
        }
    }

    /// Attempts to acquire the single-instance lock for Curry.
    /// Returns `Some(guard)` if this is the primary instance.
    /// If another instance is already running, restores/focuses its window and returns `None`.
    pub fn acquire(name: &str) -> Option<SingleInstanceGuard> {
        acquire_with_legacy(name, None)
    }

    /// Attempts to acquire the single-instance lock for Curry, while also checking/locking
    /// [LEGACY / BACKWARDS COMPATIBILITY] any legacy NotiGlow mutex so an old process and Curry cannot run concurrently.
    pub fn acquire_with_legacy(name: &str, legacy_name: Option<&str>) -> Option<SingleInstanceGuard> {
        let mut handles = Vec::new();

        // 1. Try to acquire primary mutex (e.g. Global\Curry)
        match try_acquire_named_mutex(name) {
            Some(h) => handles.push(h),
            None => {
                restore_primary_window();
                return None;
            }
        }

        // 2. [LEGACY / BACKWARDS COMPATIBILITY] If a legacy mutex name is provided (e.g. Global\NotiGlow), check/lock it as well.
        // If the legacy mutex is already held, an old process is running; focus its window and exit.
        if let Some(legacy) = legacy_name {
            match try_acquire_named_mutex(legacy) {
                Some(h) => handles.push(h),
                None => {
                    restore_primary_window();
                    for h in handles.drain(..) {
                        unsafe {
                            CloseHandle(h);
                        }
                    }
                    return None;
                }
            }
        }

        Some(SingleInstanceGuard { handles })
    }
}

#[cfg(not(target_os = "windows"))]
pub mod platform {
    pub struct SingleInstanceGuard;

    pub fn acquire(_name: &str) -> Option<SingleInstanceGuard> {
        Some(SingleInstanceGuard)
    }

    pub fn acquire_with_legacy(_name: &str, _legacy_name: Option<&str>) -> Option<SingleInstanceGuard> {
        Some(SingleInstanceGuard)
    }
}

pub use platform::{acquire, acquire_with_legacy, SingleInstanceGuard};
