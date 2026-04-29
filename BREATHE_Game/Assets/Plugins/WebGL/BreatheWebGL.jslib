mergeInto(LibraryManager.library, {
  BreatheWebGL_ReloadPage: function () {
    if (typeof window !== "undefined" && window.location && window.location.reload) {
      window.location.reload();
    }
  },

  // ============================================================
  // WebGL Microphone Support via Web Audio API
  // ============================================================

  BreatheWebGL_MicState: {
    audioContext: null,
    analyser: null,
    microphone: null,
    dataArray: null,
    isRecording: false,
    lastRms: 0,
    permissionState: 0, // 0=unknown, 1=requesting, 2=granted, 3=denied
    errorMessage: ""
  },

  BreatheWebGL_StartMicrophone: function () {
    var state = _BreatheWebGL_MicState;

    if (state.isRecording) {
      return 1; // Already recording
    }

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      state.permissionState = 3;
      state.errorMessage = "getUserMedia not supported";
      console.error("[BreatheWebGL] getUserMedia not supported in this browser");
      return 0;
    }

    state.permissionState = 1; // Requesting

    navigator.mediaDevices.getUserMedia({ audio: true, video: false })
      .then(function (stream) {
        try {
          // Create audio context (handle Safari prefix)
          var AudioContext = window.AudioContext || window.webkitAudioContext;
          state.audioContext = new AudioContext();

          // Create analyser node
          state.analyser = state.audioContext.createAnalyser();
          state.analyser.fftSize = 256;
          state.analyser.smoothingTimeConstant = 0.3;

          // Connect microphone to analyser
          state.microphone = state.audioContext.createMediaStreamSource(stream);
          state.microphone.connect(state.analyser);

          // Allocate buffer for time-domain data
          state.dataArray = new Float32Array(state.analyser.fftSize);

          state.isRecording = true;
          state.permissionState = 2; // Granted
          state.errorMessage = "";

          console.log("[BreatheWebGL] Microphone started successfully");

          // Start amplitude polling loop
          function updateAmplitude() {
            if (!state.isRecording) return;

            state.analyser.getFloatTimeDomainData(state.dataArray);

            // Compute RMS
            var sum = 0;
            for (var i = 0; i < state.dataArray.length; i++) {
              sum += state.dataArray[i] * state.dataArray[i];
            }
            state.lastRms = Math.sqrt(sum / state.dataArray.length);

            requestAnimationFrame(updateAmplitude);
          }
          updateAmplitude();

        } catch (e) {
          state.permissionState = 3;
          state.errorMessage = e.message || "AudioContext error";
          console.error("[BreatheWebGL] AudioContext setup failed:", e);
        }
      })
      .catch(function (err) {
        state.permissionState = 3;
        if (err.name === "NotAllowedError" || err.name === "PermissionDeniedError") {
          state.errorMessage = "Microphone permission denied";
        } else if (err.name === "NotFoundError" || err.name === "DevicesNotFoundError") {
          state.errorMessage = "No microphone found";
        } else {
          state.errorMessage = err.message || "Unknown error";
        }
        console.error("[BreatheWebGL] Microphone access failed:", err);
      });

    return 1;
  },

  BreatheWebGL_StopMicrophone: function () {
    var state = _BreatheWebGL_MicState;

    state.isRecording = false;

    if (state.microphone) {
      state.microphone.disconnect();
      state.microphone = null;
    }

    if (state.audioContext) {
      state.audioContext.close().catch(function () { });
      state.audioContext = null;
    }

    state.analyser = null;
    state.dataArray = null;
    state.lastRms = 0;

    console.log("[BreatheWebGL] Microphone stopped");
  },

  BreatheWebGL_GetMicAmplitude: function () {
    return _BreatheWebGL_MicState.lastRms;
  },

  BreatheWebGL_IsMicRecording: function () {
    return _BreatheWebGL_MicState.isRecording ? 1 : 0;
  },

  BreatheWebGL_GetMicPermissionState: function () {
    return _BreatheWebGL_MicState.permissionState;
  },

  BreatheWebGL_GetMicErrorMessage: function () {
    var msg = _BreatheWebGL_MicState.errorMessage || "";
    var bufferSize = lengthBytesUTF8(msg) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(msg, buffer, bufferSize);
    return buffer;
  },

  BreatheWebGL_IsMicSupported: function () {
    return (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) ? 1 : 0;
  }
});
