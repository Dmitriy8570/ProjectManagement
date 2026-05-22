import axios from 'axios'

const client = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' }
})

client.interceptors.response.use(
  r => r,
  err => {
    const data = err.response?.data
    const msg = data?.detail || data?.title || err.message || 'Unknown error'
    return Promise.reject(new Error(msg))
  }
)

export default client
