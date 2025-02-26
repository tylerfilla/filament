async function decodeAndDisplayTIFF(source, targetElement) {
    if (!targetElement) {
        throw new Error("targetElement must be provided.");
    }

    // 1. Load the TIFF data.
    let arrayBuffer;

    if (typeof source === 'string') {
        // Load from URL
        try {
            const response = await fetch(source);
            if (!response.ok) {
                throw new Error(`Failed to fetch TIFF image from ${source}: ${response.status} ${response.statusText}`);
            }
            arrayBuffer = await response.arrayBuffer();
        } catch (error) {
            throw new Error(`Error fetching TIFF image: ${error.message}`);
        }
    } else if (source instanceof File || source instanceof Blob) {
        // Load from File object
        arrayBuffer = await new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = (event) => resolve(event.target.result);
            reader.onerror = (error) => reject(error);
            reader.readAsArrayBuffer(source);
        });
    } else {
        throw new Error("Invalid source type.  Must be a URL string or a File object.");
    }

    // 2. Use a TIFF decoding library (UTIF.js).  We'll use UTIF.js for this.
    if (typeof UTIF === 'undefined') {
        throw new Error("UTIF.js library not found.  Please include it in your HTML.");
    }

    let ifds;
    try {
        ifds = UTIF.decode(arrayBuffer);
    } catch (error) {
        throw new Error(`Error decoding TIFF image: ${error.message}`);
    }

    // 3. Process each Image File Directory (IFD) (TIFF can contain multiple images).
    for (let i = 0; i < ifds.length; i++) {
        const ifd = ifds[i];

        // 4. Decode the image data (UTIF.js handles different compression methods).
        try {
            UTIF.decodeImage(arrayBuffer, ifd, ifds); // Decode the specific image
        } catch (decodeImageError) {
            console.error(`Error decoding image ${i + 1} in TIFF:`, decodeImageError);
            continue; // Try the next image if this one fails
        }

        // 5. Convert to RGBA data (for display on a canvas).
        const rgba = UTIF.toRGBA8(ifd); // Convert image to RGBA

        // 6. Create a canvas and draw the image data.
        const canvas = document.createElement('canvas');
        canvas.width = ifd.width;
        canvas.height = ifd.height;
        const ctx = canvas.getContext('2d');

        // Create an ImageData object.
        const imageData = ctx.createImageData(ifd.width, ifd.height);
        imageData.data.set(rgba); // Copy RGBA data to ImageData

        // Put ImageData onto the canvas.
        ctx.putImageData(imageData, 0, 0);

        // 7. Create an <img> element and set its source to the canvas's data URL.
        const img = document.createElement('img');
        img.src = canvas.toDataURL(); // Convert canvas to a data URL

        // 8. Display the image.
        if (targetElement.tagName.toLowerCase() === 'img') {
            // If targetElement is an <img>, replace the src. Only display the first image.
            targetElement.src = img.src;
            return; // Exit after displaying the first image in the <img> tag.
        } else {
            // If targetElement is not an <img>, append the new image.
            targetElement.appendChild(img);
        }
    }
}

async function zip(buffer) {
    try {
        const zip = await JSZip.loadAsync(buffer);
        const outputDiv = document.getElementById('output');
        outputDiv.innerHTML = '';
        
        zip.forEach(async (relativePath, zipEntry) => {
            if (!zipEntry.dir && zipEntry.name.toLowerCase().endsWith('.tif')) {
                const blob = await zipEntry.async('blob');
                decodeAndDisplayTIFF(blob, outputDiv);
            }
        });
        console.log('Successfully extracted and displayed PPM files.');
    } catch (error) {
        console.error('Extraction failed:', error);
        const outputDiv = document.getElementById('output');
        outputDiv.innerHTML = "<p style='color:red;'>Extraction failed: " + error.message + "</p>";
    }
}


async function fetchZipFromQueryParam() {
    const urlParams = new URLSearchParams(window.location.search);
    const zipUrlString = urlParams.get("src");

    if (!zipUrlString) {
        throw new Error(`Query parameter '${paramName}' not found in the URL.`);
    }

    let zipUrl;
    try {
        zipUrl = new URL(zipUrlString);
    } catch (error) {
        throw new Error(`Invalid URL provided in query parameter '${paramName}': ${error.message}`);
    }

    try {
        const response = await fetch(zipUrl);

        if (!response.ok) {
            throw new Error(`HTTP error! Status: ${response.status} ${response.statusText}`);
        }

        // Check Content-Type to make sure it's a zip file.
        const contentType = response.headers.get("Content-Type");
        if (!contentType || !contentType.startsWith("application/zip") && !contentType.startsWith('application/x-zip-compressed')) {
            const providedDetails = contentType ? ` Got content type: ${contentType}.` : " No content type was given."
            throw new Error(`Invalid content type.  Expected application/zip or application/x-zip-compressed.${providedDetails}`);
        }
        const arrayBuffer = await response.arrayBuffer();
        return arrayBuffer;

    } catch (error) {
        console.error("Error fetching or processing zip file:", error);
        throw error; // Re-throw so the caller can handle it.
    }
}
