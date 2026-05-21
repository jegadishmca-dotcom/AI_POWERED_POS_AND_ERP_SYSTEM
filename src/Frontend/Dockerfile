# Stage 1: Build the React app
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

# Stage 2: Serve the app with Nginx
FROM nginx:alpine
# Copy built assets from builder
COPY --from=builder /app/dist /usr/share/nginx/html
# Copy custom nginx config to route all requests to index.html
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Expose port 80
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
