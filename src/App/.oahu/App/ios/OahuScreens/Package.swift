// swift-tools-version:6.0
import PackageDescription

let package = Package(
    name: "OahuScreens",
    platforms: [.iOS("17.0")],
    products: [.library(name: "OahuScreens", type: .dynamic, targets: ["OahuScreens"])],
    dependencies: [.package(path: "../../../../../../../oahu-feed/swift/0.1.0-preview.g104c58c2/OahuRuntime")],
    targets: [.target(name: "OahuScreens", dependencies: [
        .product(name: "OahuRuntime", package: "OahuRuntime"),
    ])]
)
