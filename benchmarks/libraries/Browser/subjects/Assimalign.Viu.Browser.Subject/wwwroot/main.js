import { dom } from './_content/Assimalign.Viu.Browser/viu-dom.js';
import { dotnet } from './_framework/dotnet.js';
// The measured build is unchanged. This subject-only wrapper counts actual applied command frames.
window.benchmarkCounters = { frames: 0 };
const apply = dom.applyCommandBuffer;
dom.applyCommandBuffer = memory => { window.benchmarkCounters.frames++; return apply(memory); };
const { runMain } = await dotnet.create();
await runMain();