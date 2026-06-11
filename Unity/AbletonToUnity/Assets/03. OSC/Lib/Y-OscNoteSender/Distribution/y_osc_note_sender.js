autowatch = 1;
inlets = 1;
outlets = 1;

var parentTrackApi = null; // 親トラック LiveAPI（デバイス存続中は再利用）

function padString(str) {
    var bytes = [];
    var i = 0;
    for (i = 0; i < str.length; i++) {
        bytes.push(str.charCodeAt(i));
    }
    bytes.push(0);
    while (bytes.length % 4 !== 0) {
        bytes.push(0);
    }
    return bytes;
}

function appendInt(bytes, value) {
    var v = value | 0;
    bytes.push((v >>> 24) & 255);
    bytes.push((v >>> 16) & 255);
    bytes.push((v >>> 8) & 255);
    bytes.push(v & 255);
}

// /y-osc/note（iiii）用 OSC パケットを組み立てる
function buildNotePacket(trackIndex, slotIndex, pitch, velocity) {
    var bytes = padString("/y-osc/note").concat(padString(",iiii"));
    appendInt(bytes, trackIndex);
    appendInt(bytes, slotIndex);
    appendInt(bytes, pitch);
    appendInt(bytes, velocity);
    return bytes;
}

// OSC パケットを udpsend へ送る
function sendNote(trackIndex, slotIndex, pitch, velocity) {
    outlet(0, "rawbytes", buildNotePacket(trackIndex, slotIndex, pitch, velocity));
}

// LiveAPI.get の戻り値からスカラー値を取り出す
function liveApiScalar(value) {
    if (value === null || value === undefined) {
        return "";
    }
    if (typeof value === "string" || typeof value === "number") {
        return value;
    }
    if (typeof value !== "object" || value.length === undefined) {
        return value;
    }
    if (value.length >= 2) {
        return value[1];
    }
    if (value.length > 0) {
        return value[0];
    }
    return "";
}

// LiveAPI.get の戻り値を int index として読む
function liveApiIndex(value) {
    var scalar = liveApiScalar(value);
    if (scalar === "" || scalar === null || scalar === undefined) {
        return -1;
    }
    var index = parseInt(scalar, 10);
    if (isNaN(index)) {
        return -1;
    }
    return index;
}

// デバイスが載っている親トラックの LiveAPI を返す
function getParentTrackApi() {
    if (typeof LiveAPI === "undefined") {
        return null;
    }
    try {
        if (!parentTrackApi || parentTrackApi.id <= 0) {
            parentTrackApi = new LiveAPI(function () { }, "this_device canonical_parent");
        }
        if (!parentTrackApi || parentTrackApi.id <= 0) {
            parentTrackApi = null;
            return null;
        }
        return parentTrackApi;
    } catch (err) {
        parentTrackApi = null;
        return null;
    }
}

// トラック path から live_set tracks N の N を取り出す
function parseTrackIndexFromPath(trackApi) {
    if (!trackApi) {
        return -1;
    }
    var path = "";
    try {
        path = String(trackApi.unquotedpath || trackApi.path || "");
    } catch (pathErr) {
        return -1;
    }
    var match = path.match(/live_set tracks (\d+)/);
    if (!match) {
        return -1;
    }
    var index = parseInt(match[1], 10);
    if (isNaN(index)) {
        return -1;
    }
    return index;
}

// 親トラック index と再生中 slot index を 1 回の Live API 読み取りで取得する
function readTrackMeta() {
    var trackApi = getParentTrackApi();
    if (!trackApi) {
        return { trackIndex: -1, slotIndex: -1 };
    }
    var trackIndex = parseTrackIndexFromPath(trackApi);
    if (trackIndex < 0) {
        trackIndex = liveApiIndex(trackApi.get("index"));
    }
    var slotIndex = liveApiIndex(trackApi.get("playing_slot_index"));
    return { trackIndex: trackIndex, slotIndex: slotIndex };
}

function note(pitch, velocity) {
    var pitchValue = pitch | 0;
    var velocityValue = velocity | 0;
    if (velocityValue <= 0) {
        return;
    }
    var meta = readTrackMeta();
    sendNote(meta.trackIndex, meta.slotIndex, pitchValue, velocityValue);
}

function anything() {
    var selector = messagename;
    if (selector !== "note" && selector !== "noteon" && selector !== "list") {
        return;
    }
    var args = arrayfromargs(arguments);
    if (args.length >= 2) {
        note(args[0], args[1]);
    }
}
