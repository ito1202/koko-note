import AppKit
import Carbon

final class Hotkey {
    private var reference: EventHotKeyRef?
    private var handler: EventHandlerRef?
    private(set) var status: OSStatus = noErr
    var pressed: (() -> Void)?
    init() {
        var type = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        status = InstallEventHandler(GetApplicationEventTarget(), { _, event, context in
            guard let context, let event else { return OSStatus(eventNotHandledErr) }
            var id = EventHotKeyID()
            let result = GetEventParameter(event, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID), nil,
                                          MemoryLayout<EventHotKeyID>.size, nil, &id)
            guard result == noErr, id.signature == 0x4B4F4B4F, id.id == 1 else { return OSStatus(eventNotHandledErr) }
            Unmanaged<Hotkey>.fromOpaque(context).takeUnretainedValue().pressed?()
            return noErr
        }, 1, &type, Unmanaged.passUnretained(self).toOpaque(), &handler)
        if status == noErr { retry() }
    }
    func retry() {
        guard reference == nil, handler != nil else { return }
        status = RegisterEventHotKey(UInt32(kVK_Space), UInt32(controlKey | shiftKey),
                                     EventHotKeyID(signature: 0x4B4F4B4F, id: 1), GetApplicationEventTarget(), 0, &reference)
    }
    deinit {
        if let reference { UnregisterEventHotKey(reference) }
        if let handler { RemoveEventHandler(handler) }
    }
}
