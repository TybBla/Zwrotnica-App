// Usage: node make-gif.mjs out.gif delayMs frame1.jpg[:holdMs] frame2.jpg[:holdMs] ...
// Builds an animated GIF from JPEG screenshots (all resized to the first frame's size, nearest neighbour).
import { readFileSync, writeFileSync } from "node:fs";
import jpeg from "jpeg-js";
import gifenc from "gifenc";
const { GIFEncoder, quantize, applyPalette } = gifenc;

const [out, delayArg, ...specs] = process.argv.slice(2);
const defaultDelay = Number(delayArg);
const frames = specs.map((s) => {
  const i = s.lastIndexOf(":");
  const hasHold = i > 2 && /^\d+$/.test(s.slice(i + 1));
  return { path: hasHold ? s.slice(0, i) : s, delay: hasHold ? Number(s.slice(i + 1)) : defaultDelay };
});

const decoded = frames.map((f) => ({ ...f, img: jpeg.decode(readFileSync(f.path), { useTArray: true }) }));
const W = decoded[0].img.width, H = decoded[0].img.height;

function resize(img) {
  if (img.width === W && img.height === H) return img.data;
  const outBuf = new Uint8Array(W * H * 4);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const sx = Math.floor((x * img.width) / W), sy = Math.floor((y * img.height) / H);
    const si = (sy * img.width + sx) * 4, di = (y * W + x) * 4;
    outBuf[di] = img.data[si]; outBuf[di + 1] = img.data[si + 1]; outBuf[di + 2] = img.data[si + 2]; outBuf[di + 3] = 255;
  }
  return outBuf;
}

const gif = GIFEncoder();
for (const f of decoded) {
  const rgba = resize(f.img);
  const palette = quantize(rgba, 256);
  const index = applyPalette(rgba, palette);
  gif.writeFrame(index, W, H, { palette, delay: f.delay });
}
gif.finish();
writeFileSync(out, gif.bytes());
console.log(`wrote ${out}: ${decoded.length} frames, ${W}x${H}, ${(gif.bytes().length / 1024).toFixed(0)} KB`);
