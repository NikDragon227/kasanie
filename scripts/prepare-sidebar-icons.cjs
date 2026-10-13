const path = require('node:path')
const fs = require('node:fs/promises')
const sharp = require('sharp')

// Export lightweight copies of the supplied artwork; keep originals unchanged.
const sources = [
  ['overview', '17_28_10-1'],
  ['inbox', '17_28_12-2'],
  ['squad', '17_28_13-3'],
  ['planning', '17_28_15-4'],
  ['tactics', '17_28_16-5'],
  ['development', '17_28_18-6'],
  ['training', '17_28_23-7'],
  ['medical', '17_28_24-8'],
  ['schedule', '17_28_26-9'],
  ['tournaments', '17_28_27-10'],
  ['school', '17_50_07-1'],
  ['school-teams', '17_50_09-2'],
  ['staff', '17_50_10-3'],
  ['school-players', '17_50_12-4'],
  ['school-settings', '17_50_14-5'],
  ['security', '17_50_16-6'],
]

async function main() {
  const sourceDirectory = process.argv[2]
  if (!sourceDirectory) throw new Error('Pass the directory containing the supplied PNG files.')
  const outputDirectory = path.join(__dirname, '../frontend/public/brand/navigation')
  await fs.mkdir(outputDirectory, { recursive: true })
  for (const [name, suffix] of sources) {
    const source = path.join(sourceDirectory, `Изображение ChatGPT 13 окт. 2026 г., ${suffix}.png`)
    const output = path.join(outputDirectory, `${name}.webp`)
    try {
      await fs.access(source)
    } catch (error) {
      if (error.code !== 'ENOENT') throw error
      // Previously exported icons can remain even after their source files are moved.
      await fs.access(output)
      continue
    }
    await sharp(source).resize(192, 192, { fit: 'contain' }).webp({ quality: 90 }).toFile(output)
    console.log(`${name}.webp`)
  }
}

main().catch(error => { console.error(error); process.exitCode = 1 })
