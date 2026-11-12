// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "KOKONoteMac",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "KOKONoteMac", targets: ["KOKONoteMac"])],
    targets: [
        .target(name: "KOKONoteMacCore", linkerSettings: [.linkedLibrary("sqlite3"), .linkedFramework("Security")]),
        .executableTarget(name: "KOKONoteMac", dependencies: ["KOKONoteMacCore"], linkerSettings: [.linkedFramework("AppKit"), .linkedFramework("Carbon")]),
        .testTarget(name: "KOKONoteMacTests", dependencies: ["KOKONoteMacCore"])
    ],
    swiftLanguageModes: [.v5]
)
