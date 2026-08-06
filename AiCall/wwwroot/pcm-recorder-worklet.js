class Pcm16RecorderProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    this.targetRate = 24000;
    this.accumulator = 0;
    this.samples = [];
  }

  process(inputs) {
    const input = inputs[0]?.[0];
    if (!input) return true;

    for (let index = 0; index < input.length; index++) {
      this.accumulator += this.targetRate;
      if (this.accumulator < sampleRate) continue;
      this.accumulator -= sampleRate;
      const sample = Math.max(-1, Math.min(1, input[index]));
      this.samples.push(sample < 0 ? Math.round(sample * 32768) : Math.round(sample * 32767));
    }

    if (this.samples.length >= 480) {
      const buffer = new ArrayBuffer(this.samples.length * 2);
      const view = new DataView(buffer);
      this.samples.forEach((sample, index) => view.setInt16(index * 2, sample, true));
      this.samples.length = 0;
      this.port.postMessage(buffer, [buffer]);
    }
    return true;
  }
}

registerProcessor('pcm16-recorder', Pcm16RecorderProcessor);
