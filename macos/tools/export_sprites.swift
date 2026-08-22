#!/usr/bin/swift
// macOS/ImageIO fallback for export_sprites.py when Pillow is not installed. It writes the
// exact .rgba32 format SpriteAtlas expects: int32 LE width/height, then ARGB int32 pixels.
import Foundation
import ImageIO
import CoreGraphics

func writeInt32LE(_ value: Int32, to data: inout Data) {
    var littleEndian = value.littleEndian
    withUnsafeBytes(of: &littleEndian) { data.append(contentsOf: $0) }
}

let arguments = Array(CommandLine.arguments.dropFirst())
guard arguments.count >= 2 else {
    fputs("Usage: export_sprites.swift <source.png> [...] <output-directory>\n", stderr)
    exit(1)
}

let outputDirectory = URL(fileURLWithPath: arguments.last!, isDirectory: true)
try FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)

for sourcePath in arguments.dropLast() {
    let sourceURL = URL(fileURLWithPath: sourcePath)
    guard let imageSource = CGImageSourceCreateWithURL(sourceURL as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(imageSource, 0, nil) else {
        throw NSError(domain: "MDPlayerSpriteExport", code: 1,
                      userInfo: [NSLocalizedDescriptionKey: "Cannot decode \(sourcePath)"])
    }

    let width = image.width
    let height = image.height
    var rgba = [UInt8](repeating: 0, count: width * height * 4)
    guard let context = CGContext(
        data: &rgba, width: width, height: height,
        bitsPerComponent: 8, bytesPerRow: width * 4,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue)
    else {
        throw NSError(domain: "MDPlayerSpriteExport", code: 2,
                      userInfo: [NSLocalizedDescriptionKey: "Cannot create pixel context"])
    }
    context.interpolationQuality = .none
    // The byte buffer returned by this bitmap context is already top-left row-major for
    // the format SpriteAtlas reads. Applying a CoreGraphics Y flip here inverted every
    // transport icon in the Avalonia player, so draw at native orientation.
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))

    var output = Data()
    writeInt32LE(Int32(width), to: &output)
    writeInt32LE(Int32(height), to: &output)
    for pixel in stride(from: 0, to: rgba.count, by: 4) {
        let argb = UInt32(rgba[pixel + 3]) << 24
            | UInt32(rgba[pixel]) << 16
            | UInt32(rgba[pixel + 1]) << 8
            | UInt32(rgba[pixel + 2])
        writeInt32LE(Int32(bitPattern: argb), to: &output)
    }

    let name = sourceURL.deletingPathExtension().lastPathComponent
    let destination = outputDirectory.appendingPathComponent(name).appendingPathExtension("rgba32")
    try output.write(to: destination)
    print("\(destination.path): \(output.count) bytes (\(width)x\(height))")
}
